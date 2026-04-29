using System.Collections;
using System.Security.Cryptography;
using UnityEngine;

public class JengaMaker : MonoBehaviour
{
    public int width;
    public int height;

    public int minStack;
    public int maxStack;

    public GameObject Block;

    private JengaData [,] data;


    void OnValidate()
    {
        data = new JengaData[width, height];
    }

    void Awake()
    {
        int pivot = width / 2;
        for (int i = 0; i< width; i++)
        {
            for (int j = 0; j< height; j++)
            {
                var go = Instantiate(Block, new Vector2(i - pivot + 0.5f, j + 0.5f),Quaternion.identity);
                go.transform.SetParent(transform);
                data[i, j] = new JengaData(go, i, j, Random.Range(minStack, maxStack+1));
            }
        }
    }
}

public class JengaData
{
    public int stack {get; set;}
    public int x {get; set;}
    public int y {get; set;}

    public GameObject obj {get; set;}

    public JengaData(GameObject obj, int x, int y, int stack)
    {
        this.obj = obj;
        this.x = x;
        this.y = y;

        this.stack = stack; 
        obj.GetComponent<SpriteRenderer>().color = stack ==3? Color.red : stack == 2? Color.yellow : Color.aquamarine;
    }
}