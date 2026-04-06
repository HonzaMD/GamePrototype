using Assets.Scripts.Core;
using Assets.Scripts.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu]
public class CannonSettings : ScriptableObject
{
    public Placeable Obj;
    public float Speed;
    public int Shots;
}
