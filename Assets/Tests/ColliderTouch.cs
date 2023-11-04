using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assets.Scripts.Utils;

public class ColliderTouch
{
    // A Test behaves as an ordinary method
    [Test]
    public void ColliderTouchSimplePasses()
    {
        var obj1 = new GameObject();
        SphereCollider sc = obj1.AddComponent<SphereCollider>();
        sc.center = Vector3.up;
        sc.radius = 1;

        var obj2 = new GameObject();
        BoxCollider box = obj2.AddComponent<BoxCollider>();
        box.center = Vector3.down;
        box.size = new Vector3(5, 2, 1);

        var res = sc.Touches(box, 0.1f);
        Assert.IsTrue(res);
    }

    [Test]
    public void LongColliders()
    {
        var obj1 = new GameObject();
        BoxCollider sc = obj1.AddComponent<BoxCollider>();
        sc.size = new Vector3(50, 1, 1);
        obj1.SetActive(true);

        var obj2 = new GameObject();
        BoxCollider box = obj2.AddComponent<BoxCollider>();
        box.size = new Vector3(50, 1, 1);
        obj2.transform.position = Vector3.up * 2;
        obj2.SetActive(true);

        var res = sc.Touches(box, 0.1f);
        Assert.IsFalse(res);
    }

}
