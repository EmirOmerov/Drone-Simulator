using DroneSim.CameraRig;
using DroneSim.Controls;
using DroneSim.Flight;
using DroneSim.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DroneSim.Tests
{
    /// <summary>
    /// Guards the generated scene: every reference the drone needs must be serialized, not "None".
    /// (Regression: sportTuning was saved empty, so Left Shift / R1 silently did nothing.)
    /// </summary>
    public sealed class FlightSceneWiringTests
    {
        const string ScenePath = "Assets/_Project/Scenes/FlightTest.unity";

        [OneTimeSetUp]
        public void OpenScene() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        static T Single<T>() where T : Object
        {
            var found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(found.Length, Is.EqualTo(1), $"expected exactly one {typeof(T).Name} in the scene");
            return found[0];
        }

        static Object Field(Object component, string name)
        {
            var property = new SerializedObject(component).FindProperty(name);
            Assert.That(property, Is.Not.Null, $"{component.GetType().Name} has no field '{name}'");
            return property.objectReferenceValue;
        }

        [Test]
        public void Drone_HasNormalAndSportTuning()
        {
            var controller = Single<AngleModeController>();
            Assert.That(Field(controller, "tuning"), Is.Not.Null, "normal tuning");
            Assert.That(Field(controller, "sportTuning"), Is.Not.Null, "sport tuning (Left Shift / R1)");
            Assert.That(Field(controller, "tuning"), Is.Not.SameAs(Field(controller, "sportTuning")));
        }

        [Test]
        public void ToggleComponents_AreWiredToTheInputProvider()
        {
            var provider = Single<FlightInputProvider>();
            Assert.That(Field(Single<FlightModeToggle>(), "inputProvider"), Is.SameAs(provider), "sport toggle");
            Assert.That(Field(Single<DroneCamera>(), "inputProvider"), Is.SameAs(provider), "camera toggle");
            Assert.That(Field(Single<DroneCamera>(), "target"), Is.Not.Null, "camera target");
            Assert.That(Field(Single<PauseMenu>(), "inputProvider"), Is.SameAs(provider), "pause menu");
            Assert.That(Field(Single<PauseMenu>(), "respawn"), Is.Not.Null, "pause menu reset");
            Assert.That(Field(Single<DroneRespawn>(), "inputProvider"), Is.SameAs(provider), "keyboard reset");
        }

        [Test]
        public void SportTuning_IsActuallyFaster()
        {
            var controller = Single<AngleModeController>();
            var normal = (FlightTuning)Field(controller, "tuning");
            var sport = (FlightTuning)Field(controller, "sportTuning");
            Assert.That(sport.maxTiltDegrees, Is.GreaterThan(normal.maxTiltDegrees));
            Assert.That(sport.linearDrag, Is.LessThan(normal.linearDrag));
        }
    }
}
