using Assets.Scripts.Bases;
using NUnit.Framework;
using System;
using UnityEngine;

public class EnergySettingsTests
{
    private static Array Actions => Enum.GetValues(typeof(EnergyAction));

    [Test]
    public void EveryActionHasCostField()
    {
        foreach (EnergyAction action in Actions)
        {
            var field = typeof(EnergySettings).GetField(action.ToString());
            Assert.IsNotNull(field, $"Chybi field {action}");
            Assert.AreEqual(typeof(EnergyCost), field.FieldType, $"Spatny typ fieldu {action}");
        }
    }

    [Test]
    public void IndexerReadsFieldOfSameName()
    {
        var settings = ScriptableObject.CreateInstance<EnergySettings>();
        foreach (EnergyAction action in Actions)
            typeof(EnergySettings).GetField(action.ToString()).SetValue(settings, Expected(action));

        foreach (EnergyAction action in Actions)
            Assert.AreEqual(Expected(action), settings[action], action.ToString());

        UnityEngine.Object.DestroyImmediate(settings);
    }

    private static EnergyCost Expected(EnergyAction action)
        => new EnergyCost { Cost = (int)action + 1, MinEnergy = (int)action + 100 };
}
