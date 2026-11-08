"""家膳智选本地数据接口。Unity 不携带 MySQL 凭据，所有业务记录保存到 MySQL。"""
from contextlib import contextmanager
from datetime import date
import hashlib
import hmac
import os
from pathlib import Path
import secrets
import time

from dotenv import load_dotenv
from fastapi import FastAPI, Depends, Header, HTTPException
from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError
from pydantic import BaseModel, Field, field_validator
import pymysql

load_dotenv(Path(__file__).with_name(".env"))
app = FastAPI(title="家膳智选本地接口", version="0.1.0")
# 会话只在服务内存中保存，重启服务后重新登录即可。
_sessions: dict[str, tuple[int, float]] = {}


@contextmanager
def database():
    """每个请求单独使用连接；失败回滚，避免共享连接跨线程。"""
    connection = pymysql.connect(
        host=os.getenv("MYSQL_HOST", "127.0.0.1"), port=int(os.getenv("MYSQL_PORT", "3306")),
        user=os.environ["MYSQL_USER"], password=os.environ["MYSQL_PASSWORD"],
        database=os.getenv("MYSQL_DATABASE", "jiashan_zhixuan"),
        charset="utf8mb4", cursorclass=pymysql.cursors.DictCursor,
        connect_timeout=5, read_timeout=10, write_timeout=10,
    )
    try:
        yield connection
        connection.commit()
    except Exception:
        connection.rollback()
        raise
    finally:
        connection.close()


@app.exception_handler(pymysql.MySQLError)
async def database_error(_request, _exception):
    return JSONResponse(status_code=503, content={"detail": "数据库暂时不可用，请检查本机 MySQL 服务。"})


@app.exception_handler(RequestValidationError)
async def validation_error(_request, _exception):
    return JSONResponse(status_code=422, content={"detail": "输入信息不完整或格式不正确，请检查后重试。"})


def current_user(authorization: str = Header(default="")) -> int:
    """校验登录令牌，过期后要求重新登录。"""
    token = authorization.removeprefix("Bearer ")
    session = _sessions.get(token)
    if not session or session[1] < time.time():
        _sessions.pop(token, None)
        raise HTTPException(401, "登录已失效，请重新登录。")
    return session[0]


class Credentials(BaseModel):
    username: str = Field(min_length=2, max_length=50)
    password: str = Field(min_length=8, max_length=100)


class Member(BaseModel):
    name: str = Field(min_length=1, max_length=100)
    age: int = Field(ge=0, le=120)
    goal: str = Field(default="日常均衡", max_length=100)
    allergies: str = Field(default="", max_length=1000)
    restrictions: str = Field(default="", max_length=1000)
    dislikes: str = Field(default="", max_length=1000)
    preferences: str = Field(default="", max_length=1000)


class Family(BaseModel):
    members: list[Member] = Field(max_length=12)


class Meal(BaseModel):
    food: str = Field(min_length=1, max_length=100)
    date: date
    meal: str
    calories: float = Field(ge=0, le=100000, allow_inf_nan=False)
    carbs: float = Field(ge=0, le=100000, allow_inf_nan=False)
    protein: float = Field(ge=0, le=100000, allow_inf_nan=False)
    fat: float = Field(ge=0, le=100000, allow_inf_nan=False)

    @field_validator("meal")
    @classmethod
    def valid_meal(cls, value):
        if value not in ("早餐", "午餐", "晚餐", "加餐"):
            raise ValueError("无效餐次")
        return value


def members_for(connection, user_id):
    with connection.cursor() as cursor:
        cursor.execute("SELECT name,age,goal,allergies,restrictions,dislikes,preferences FROM family_members WHERE user_id=%s ORDER BY position", (user_id,))
        return cursor.fetchall()


def login_result(connection, user_id, username):
    # 清理过期会话，令牌有效期 24 小时。
    for key, value in list(_sessions.items()):
        if value[1] < time.time():
            _sessions.pop(key, None)
    token = secrets.token_urlsafe(32)
    _sessions[token] = (user_id, time.time() + 86400)
    return {"token": token, "username": username, "members": members_for(connection, user_id)}


@app.get("/health")
def health():
    with database() as connection, connection.cursor() as cursor:
        cursor.execute("SELECT 1")
        return {"status": "ok", "database": "MySQL"}


@app.post("/auth/register")
def register(data: Credentials):
    salt = secrets.token_hex(16)
    digest = hashlib.pbkdf2_hmac("sha256", data.password.encode(), bytes.fromhex(salt), 310000).hex()
    with database() as connection, connection.cursor() as cursor:
        try:
            cursor.execute("INSERT INTO users(username,password_salt,password_hash) VALUES(%s,%s,%s)", (data.username, salt, digest))
        except pymysql.err.IntegrityError:
            raise HTTPException(409, "这个用户名已注册，请直接登录或更换用户名。")
        return login_result(connection, cursor.lastrowid, data.username)


@app.post("/auth/login")
def login(data: Credentials):
    with database() as connection, connection.cursor() as cursor:
        cursor.execute("SELECT id,username,password_salt,password_hash FROM users WHERE username=%s", (data.username,))
        user = cursor.fetchone()
        salt = bytes.fromhex(user["password_salt"]) if user else bytes(16)
        digest = hashlib.pbkdf2_hmac("sha256", data.password.encode(), salt, 310000).hex()
        if not user or not hmac.compare_digest(digest, user["password_hash"]):
            raise HTTPException(401, "用户名或密码不正确。")
        return login_result(connection, user["id"], user["username"])


@app.get("/family")
def get_family(user_id: int = Depends(current_user)):
    with database() as connection:
        return members_for(connection, user_id)


@app.put("/family")
def put_family(data: Family, user_id: int = Depends(current_user)):
    with database() as connection, connection.cursor() as cursor:
        cursor.execute("DELETE FROM family_members WHERE user_id=%s", (user_id,))
        for position, member in enumerate(data.members):
            cursor.execute("INSERT INTO family_members(user_id,position,name,age,goal,allergies,restrictions,dislikes,preferences) VALUES(%s,%s,%s,%s,%s,%s,%s,%s,%s)", (user_id, position, member.name, member.age, member.goal, member.allergies, member.restrictions, member.dislikes, member.preferences))
    return {"saved": len(data.members)}


@app.get("/diary")
def get_diary(user_id: int = Depends(current_user)):
    with database() as connection, connection.cursor() as cursor:
        cursor.execute("SELECT food,DATE_FORMAT(eaten_on,'%%Y-%%m-%%d') AS date,meal,calories,carbs,protein,fat FROM meal_records WHERE user_id=%s ORDER BY eaten_on DESC,id DESC", (user_id,))
        return cursor.fetchall()


@app.post("/diary")
def post_diary(data: Meal, user_id: int = Depends(current_user)):
    with database() as connection, connection.cursor() as cursor:
        cursor.execute("INSERT INTO meal_records(user_id,food,eaten_on,meal,calories,carbs,protein,fat) VALUES(%s,%s,%s,%s,%s,%s,%s,%s)", (user_id, data.food, data.date, data.meal, data.calories, data.carbs, data.protein, data.fat))
        return {"id": cursor.lastrowid}
