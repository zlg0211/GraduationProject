using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CodeHabits : MonoBehaviour
{
    //重要或难懂的变量、属性、方法均需要注释，简单且不重要的变量就不用注释了。

    [Header("暴露在Inspector的变量使用Header属性")] [SerializeField]
    private int exampleInt = 0;

    private int _privateInt = 0; // 私有变量使用下划线开头，驼峰命名法

    /// <summary>
    /// 属性使用xml注释
    /// </summary>
    public int PublicInt => _privateInt;

    /// <summary>
    /// 方法注释使用xml注释格式，重要参数使用param标签描述
    /// </summary>
    /// <param name="i"></param>
    public void Method0(int i)
    {
        //若if语句的条件很短并且只有一行代码，则省略大括号，for、while、foreach同理
        if (i == 0) return;
        _privateInt = i;
    }

    /// <summary>
    /// 只有一行代码的函数使用=>语法糖，减少代码行数
    /// </summary>
    public void Method1() => Method0(1);
}