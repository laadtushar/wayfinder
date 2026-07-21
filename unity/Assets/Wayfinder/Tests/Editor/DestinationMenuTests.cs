using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wayfinder.Core;
using Wayfinder.Unity;

namespace Wayfinder.Unity.Tests
{
    /// EditMode tests for the viewscreen destination list. Drives BuildMenu
    /// directly (no scene, no Awake) so behavior is testable headless.
    public class DestinationMenuTests
    {
        GameObject _root;
        DestinationMenu _menu;

        [SetUp]
        public void CreateMenu()
        {
            _root = new GameObject("MenuRoot", typeof(RectTransform));
            _menu = _root.AddComponent<DestinationMenu>();
            var container = new GameObject("Container", typeof(RectTransform));
            container.transform.SetParent(_root.transform, false);

            var so = new UnityEditor.SerializedObject(_menu);
            so.FindProperty("entryContainer").objectReferenceValue = container.GetComponent<RectTransform>();
            so.FindProperty("viewingDistanceMeters").floatValue = 1.9f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void DestroyMenu()
        {
            Object.DestroyImmediate(_root);
        }

        static WorldRegistry ThreeWorlds()
        {
            return new WorldRegistry(new List<WorldDefinition>
            {
                new WorldDefinition("w-a", "World A", "Site_A", 3.7f),
                new WorldDefinition("w-b", "World B", "Site_B", 1.6f),
                new WorldDefinition("w-c", "World C", "Site_C", 9.8f),
            });
        }

        [Test]
        public void BuildMenu_Creates_One_Entry_Per_World_In_Registry_Order()
        {
            _menu.BuildMenu(ThreeWorlds());

            Assert.AreEqual(3, _menu.Entries.Count);
            Assert.AreEqual("w-a", _menu.Entries[0].GetComponent<WorldIdHolder>().WorldId);
            Assert.AreEqual("w-b", _menu.Entries[1].GetComponent<WorldIdHolder>().WorldId);
            Assert.AreEqual("w-c", _menu.Entries[2].GetComponent<WorldIdHolder>().WorldId);
        }

        [Test]
        public void Entries_Meet_The_Android_XR_Minimum_Target_Size_At_Viewing_Distance()
        {
            _menu.BuildMenu(ThreeWorlds());

            float minimum = InteractionTargets.MinimumSizeMeters(1.9f);
            foreach (var entry in _menu.Entries)
            {
                var rect = (RectTransform)entry.transform;
                Assert.GreaterOrEqual(rect.sizeDelta.y, minimum,
                    entry.name + " shorter than the Android XR minimum target size");
            }
        }

        [Test]
        public void Select_Raises_WorldSelected_With_The_World_Id()
        {
            _menu.BuildMenu(ThreeWorlds());
            string received = null;
            _menu.WorldSelected += id => received = id;

            _menu.Select("w-b");

            Assert.AreEqual("w-b", received);
            Assert.AreEqual("w-b", _menu.SelectedWorldId);
        }

        [Test]
        public void Select_Highlights_Only_The_Selected_Entry()
        {
            _menu.BuildMenu(ThreeWorlds());
            _menu.Select("w-b");

            var normalOfSelected = _menu.Entries[1].colors.normalColor;
            var normalOfOther = _menu.Entries[0].colors.normalColor;
            Assert.AreNotEqual(normalOfOther, normalOfSelected,
                "selected entry not visually distinct");
        }

        [Test]
        public void Rebuilding_The_Menu_Clears_Selection_And_Old_Entries()
        {
            _menu.BuildMenu(ThreeWorlds());
            _menu.Select("w-a");

            _menu.BuildMenu(ThreeWorlds());

            Assert.IsNull(_menu.SelectedWorldId);
            Assert.AreEqual(3, _menu.Entries.Count);
        }
    }
}
