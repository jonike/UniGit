﻿using System.IO;
using System.Linq;
using Assets.Plugins.UniGit.Editor.Hooks;
using LibGit2Sharp;
using UniGit.Adapters;
using UniGit.Settings;
using UniGit.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace UniGit
{
	[InitializeOnLoad]
	public static class UniGitLoader
	{
		public static GitManager GitManager;
		private static readonly InjectionHelper injectionHelper;
		public static GitCallbacks GitCallbacks;
		private static GitReflectionHelper ReflectionHelper;
		private static GitSettingsJson GitSettings;

		static UniGitLoader()
		{
			Profiler.BeginSample("UniGit Initialization");
			try
			{
				GitWindows.OnWindowAddedEvent += OnWindowAdded;
				EditorApplication.update += OnEditorUpdate;

				injectionHelper = new InjectionHelper();

				GitWindows.Init();

				string repoPath = Application.dataPath.Replace(UniGitPath.UnityDeirectorySeparatorChar + "Assets", "").Replace(UniGitPath.UnityDeirectorySeparatorChar, Path.DirectorySeparatorChar);
				string settingsPath = UniGitPath.Combine(repoPath, ".git", "UniGit", "Settings.json");
				string logPath = UniGitPath.Combine(repoPath, ".git", "UniGit", "log.txt");

				injectionHelper.Bind<string>().FromInstance(repoPath).WithId("repoPath");
				injectionHelper.Bind<string>().FromInstance(settingsPath).WithId("settingsPath");
				injectionHelper.Bind<string>().FromInstance(logPath).WithId("logPath");

				injectionHelper.Bind<UniGitData>().FromMethod(GetUniGitData).NonLazy();
				injectionHelper.Bind<GitCallbacks>().FromMethod(h =>
				{
					var c = new GitCallbacks();
					EditorApplication.update += c.IssueEditorUpdate;
					c.RefreshAssetDatabase += AssetDatabase.Refresh;
					c.SaveAssetDatabase += AssetDatabase.SaveAssets;
					EditorApplication.projectWindowItemOnGUI += c.IssueProjectWindowItemOnGUI;
					//asset postprocessing
					GitAssetPostprocessors.OnWillSaveAssetsEvent += c.IssueOnWillSaveAssets;
					GitAssetPostprocessors.OnPostprocessImportedAssetsEvent += c.IssueOnPostprocessImportedAssets;
					GitAssetPostprocessors.OnPostprocessDeletedAssetsEvent += c.IssueOnPostprocessDeletedAssets;
					GitAssetPostprocessors.OnPostprocessMovedAssetsEvent += c.IssueOnPostprocessMovedAssets;
					return c;
				});
				injectionHelper.Bind<IGitPrefs>().To<UnityEditorGitPrefs>();
				injectionHelper.Bind<GitManager>().NonLazy();
				injectionHelper.Bind<GitSettingsJson>();
				injectionHelper.Bind<GitSettingsManager>();
				injectionHelper.Bind<GitAsyncManager>();
				injectionHelper.Bind<GitFileWatcher>().NonLazy();
				injectionHelper.Bind<GitReflectionHelper>();
				injectionHelper.Bind<IGitResourceManager>().To<GitResourceManager>();
				injectionHelper.Bind<GitOverlay>();
				injectionHelper.Bind<GitAutoFetcher>().NonLazy();
				injectionHelper.Bind<GitLog>();
				injectionHelper.Bind<ILogger>().FromMethod(h => new Logger(h.GetInstance<GitLog>()));
				injectionHelper.Bind<GitAnimation>();

				//credentials
				injectionHelper.Bind<ICredentialsAdapter>().To<WincredCredentialsAdapter>();
				injectionHelper.Bind<GitCredentialsManager>().NonLazy();
				//externals
				injectionHelper.Bind<IExternalAdapter>().To<GitExtensionsAdapter>();
				injectionHelper.Bind<IExternalAdapter>().To<TortoiseGitAdapter>();
				injectionHelper.Bind<GitExternalManager>();
				//must be non lazy as it add itself as a filter
				injectionHelper.Bind<GitLfsManager>().NonLazy();
				//hooks
				injectionHelper.Bind<GitPushHookBase>().To<GitLfsPrePushHook>();
				injectionHelper.Bind<GitHookManager>().NonLazy();
				//helpers
				injectionHelper.Bind<GitLfsHelper>();
				injectionHelper.Bind<FileLinesReader>();
				//project window overlays
				injectionHelper.Bind<GitProjectOverlay>().NonLazy();

				if (Repository.IsValid(repoPath))
				{
					Rebuild(injectionHelper);
				}
			}
			finally
			{
				Profiler.EndSample();
			}
		}

		private static void Rebuild(InjectionHelper injectionHelper)
		{
			var settingsManager = injectionHelper.GetInstance<GitSettingsManager>();
			settingsManager.LoadGitSettings();

			//delayed called must be used for serialized properties to be loaded
			EditorApplication.delayCall += () =>
			{
				settingsManager.LoadOldSettingsFile();
			};

			GitManager = injectionHelper.GetInstance<GitManager>();
			GitCallbacks = injectionHelper.GetInstance<GitCallbacks>();
			ReflectionHelper = injectionHelper.GetInstance<GitReflectionHelper>();
			GitSettings = injectionHelper.GetInstance<GitSettingsJson>();

			GitCallbacks.RepositoryCreate += OnRepositoryCreate;
			GitCallbacks.OnLogEntry += OnLogEntry;
			var uniGitData = injectionHelper.GetInstance<UniGitData>();
			uniGitData.OnBeforeReloadAction = OnBeforeAssemblyReload;

			injectionHelper.CreateNonLazy();

			GitProjectContextMenus.Init(GitManager, injectionHelper.GetInstance<GitExternalManager>(),GitCallbacks);
			GitUnityMenu.Init(GitManager);
		}

		private static void OnWindowAdded(EditorWindow editorWindow)
		{
			injectionHelper.Inject(editorWindow);
			editorWindow.Repaint();
		}

		//emulate Unity's delayed call
		private static void OnEditorUpdate()
		{
			GitCallbacks.IssueDelayCall(true);
		}

		private static void OnRepositoryCreate()
		{
			Rebuild(injectionHelper);
			foreach (var window in GitWindows.Windows)
			{
				injectionHelper.Inject(window);
				window.Repaint();
			}
		}

		private static void OnBeforeAssemblyReload()
		{
			if(!(bool)ReflectionHelper.TestRunningField.GetValue(null))
				injectionHelper.Dispose();
		}

		private static void OnLogEntry(GitLog.LogEntry logEntry)
		{
			if (!GitSettings.UseUnityConsole)
				GetWindow<GitLogWindow>();
		}

		private static UniGitData GetUniGitData(InjectionHelper injectionHelper)
		{
			var existentData = Resources.FindObjectsOfTypeAll<UniGitData>();
			foreach (var data in existentData)
			{
				if (data.Initialized) return data;
			}
			return existentData.Length > 0 ? existentData[0] : ScriptableObject.CreateInstance<UniGitData>();
		}

	    public static T FindWindow<T>() where T : EditorWindow
	    {
	        var editorWindow = Resources.FindObjectsOfTypeAll<T>().FirstOrDefault();
	        if (editorWindow != null)
	        {
	            return editorWindow;
	        }
	        return null;
	    }

	    public static T GetWindow<T>() where T : EditorWindow
	    {
	        return GetWindow<T>(false);
	    }

	    public static T GetWindow<T>(bool utility) where T : EditorWindow
	    {
	        var editorWindow = Resources.FindObjectsOfTypeAll<T>().FirstOrDefault();
	        if (editorWindow != null)
	        {
		        editorWindow.Show();
				return editorWindow;
	        }
	        var newWindow = ScriptableObject.CreateInstance<T>();
            if(utility)
                newWindow.ShowUtility();
            else
	            newWindow.Show();

            return newWindow;
	    }

	    public static T DisplayWizard<T>(string title, string createButtonName) where T : ScriptableWizard
	    {
	        return DisplayWizard<T>(title, createButtonName, "");
	    }

	    public static T DisplayWizard<T>(string title,string createButtonName,string otherButtonName) where T : ScriptableWizard
	    {
	        var instance = ScriptableWizard.DisplayWizard<T>(title, createButtonName, otherButtonName);
            return instance;
        }
	}
}