using UnityEditor;
using UnityEngine;

namespace Editor
{
    [InitializeOnLoad]
    public static class CoopInputSetup
    {
        static CoopInputSetup()
        {
            EditorApplication.delayCall += () => Create(silent: true);
        }

        [MenuItem("Tools/Co-op/Create Gamepad Axes")]
        private static void CreateFromMenu()
        {
            Create(silent: false);
        }

        private static void Create(bool silent)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
            if (assets == null || assets.Length == 0)
                return;

            var manager = new SerializedObject(assets[0]);
            var axes = manager.FindProperty("m_Axes");
            bool changed = false;

            for (int joystick = 1; joystick <= 8; ++joystick)
            {
                changed |= EnsureAxis(axes, "CoopJoy" + joystick + "X", joystick, axis: 0, invert: false);
                changed |= EnsureAxis(axes, "CoopJoy" + joystick + "Y", joystick, axis: 1, invert: true);
                changed |= EnsureAxis(axes, "CoopJoy" + joystick + "RX", joystick, axis: 3, invert: false);
                changed |= EnsureAxis(axes, "CoopJoy" + joystick + "RY", joystick, axis: 4, invert: true);
                changed |= EnsureAxis(axes, "CoopJoy" + joystick + "DX", joystick, axis: 5, invert: false);
                changed |= EnsureAxis(axes, "CoopJoy" + joystick + "DY", joystick, axis: 6, invert: false);
                changed |= EnsureAxis(axes, "CoopJoy" + joystick + "LT", joystick, axis: 8, invert: false);
                changed |= EnsureAxis(axes, "CoopJoy" + joystick + "RT", joystick, axis: 9, invert: false);
            }

            if (!changed)
            {
                if (!silent)
                    Debug.Log("Co-op gamepad axes already exist");
                return;
            }

            manager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("Co-op gamepad axes created: CoopJoy1X/Y .. CoopJoy8X/Y");
        }

        private static bool EnsureAxis(SerializedProperty axes, string name, int joystick, int axis, bool invert)
        {
            for (int i = 0; i < axes.arraySize; ++i)
            {
                if (axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue == name)
                    return false;
            }

            axes.arraySize += 1;
            var element = axes.GetArrayElementAtIndex(axes.arraySize - 1);
            element.FindPropertyRelative("m_Name").stringValue = name;
            element.FindPropertyRelative("descriptiveName").stringValue = "";
            element.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
            element.FindPropertyRelative("negativeButton").stringValue = "";
            element.FindPropertyRelative("positiveButton").stringValue = "";
            element.FindPropertyRelative("altNegativeButton").stringValue = "";
            element.FindPropertyRelative("altPositiveButton").stringValue = "";
            element.FindPropertyRelative("gravity").floatValue = 0f;
            element.FindPropertyRelative("dead").floatValue = 0.2f;
            element.FindPropertyRelative("sensitivity").floatValue = 1f;
            element.FindPropertyRelative("snap").boolValue = false;
            element.FindPropertyRelative("invert").boolValue = invert;
            element.FindPropertyRelative("type").intValue = 2;
            element.FindPropertyRelative("axis").intValue = axis;
            element.FindPropertyRelative("joyNum").intValue = joystick;
            return true;
        }
    }
}
