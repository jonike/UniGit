﻿using UniGit.Utils;
using UnityEditor;
using UnityEngine;
#pragma warning disable 618

namespace UniGit.Inspectors
{
	[CustomEditor(typeof(GitSettings))]
	public class GitSettingsInspector : Editor
	{
		public override void OnInspectorGUI()
		{
			EditorGUILayout.HelpBox("Open the 'Git Settings' window to change the settings.",MessageType.Info);
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(new GUIContent("Open Settings"), GitGUI.Styles.AddComponentBtn))
			{
				GitSettingsWindow.CreateEditor();
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}
	}
}