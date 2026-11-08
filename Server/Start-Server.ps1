$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if (-not (Test-Path -LiteralPath '.venv/Scripts/python.exe')) {
    & uv venv .venv
    & uv pip install --python .venv/Scripts/python.exe -r requirements.txt
}
& ./.venv/Scripts/python.exe -m uvicorn app:app --host 127.0.0.1 --port 8766
