﻿using System;
using LibGit2Sharp;
using UnityEngine;

namespace UniGit.Status
{
	[Serializable]
	public class GitStatusRemoteEntry
	{
		[SerializeField] private string url;
		[SerializeField] private string name;

		public GitStatusRemoteEntry(Remote remote)
		{
			url = remote.Url;
			name = remote.Name;
		}

		public string Url
		{
			get { return url; }
		}

		public string Name
		{
			get { return name; }
		}
	}
}
