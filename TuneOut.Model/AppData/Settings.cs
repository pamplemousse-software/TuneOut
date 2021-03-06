﻿using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace TuneOut.AppData
{
	/// <summary>
	/// Manages settings that the client can manipulate.
	/// </summary>
	public static class Settings
	{
		private static Windows.Storage.ApplicationDataContainer _local = GetLocalSettings();

		private static Windows.Storage.ApplicationDataContainer GetLocalSettings()
		{
			Contract.Assume(Windows.Storage.ApplicationData.Current != null);
			return Windows.Storage.ApplicationData.Current.LocalSettings;
		}

		/// <summary>
		/// Gets or sets the last time the iTunes Library was cached into local storage.
		/// </summary>
		internal static DateTimeOffset LastLibraryUpdate
		{
			get
			{
				return GetValue<DateTimeOffset>("LastLibraryResync");
			}

			set
			{
				_local.Values["LastLibraryResync"] = value;
			}
		}

		/// <summary>
		/// Gets or sets whether the first-run wizard has run.
		/// </summary>
		public static bool IsFirstRunComplete
		{
			get
			{
				return GetValue<bool>("IsFirstRunComplete");
			}

			set
			{
				_local.Values["IsFirstRunComplete"] = value;
			}
		}

		/// <summary>
		/// Gets or sets the <seealso cref="Guid"/> that labels the current <seealso cref="Windows.Storage.ApplicationDataContainer"/> used for album artwork.
		/// If the stored value is equal to <seealso cref="Guid.Empty"/>, a new value will be generated and stored on the next request.
		/// </summary>
		internal static Guid ArtContainerGuid
		{
			get
			{
				Guid current = GetValue<Guid>("ArtGuid");

				if (current == Guid.Empty)
				{
					current = Guid.NewGuid();
					_local.Values["ArtGuid"] = current;
				}

				return current;
			}

			set
			{
				Guid current = GetValue<Guid>("ArtGuid");

				if (current != value)
				{
					_local.DeleteContainer(ArtContainerGuid.ToString());
					_local.Values["ArtGuid"] = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets whether album artwork should be downloaded regardless of Internet connection settings.
		/// </summary>
		public static bool IsInternetPolicyIgnored
		{
			get
			{
				return GetValue<bool>("ForceDownload");
			}

			set
			{
				_local.Values["ForceDownload"] = value;
			}
		}

		/// <summary>
		/// Gets or sets volume on a 0.0 to 100.0 scale, where 100.0 is the loudest.
		/// </summary>
		public static double Volume
		{
			get
			{
				return GetValue<double>("Volume", 100d);
			}

			set
			{
				_local.Values["Volume"] = value;
			}
		}

		/// <summary>
		/// Gets or sets whether the audio should be muted.
		/// </summary>
		public static bool IsMuted
		{
			get
			{
				return GetValue<bool>("IsMuted");
			}

			set
			{
				_local.Values["IsMuted"] = value;
			}
		}

		private static T GetValue<T>(string key) where T : struct
		{
			if (_local.Values.ContainsKey(key))
			{
				return (_local.Values[key] as T?).GetValueOrDefault();
			}

			return default(T);
		}

		private static T GetValue<T>(string key, T defaultValue) where T : struct
		{
			if (_local.Values.ContainsKey(key))
			{
				var nullable = _local.Values[key] as T?;
				if (nullable.HasValue)
				{
					return nullable.Value;
				}
			}

			return defaultValue;
		}

		private static T GetObject<T>(string key) where T : class
		{
			if (_local.Values.ContainsKey(key))
			{
				return _local.Values[key] as T;
			}

			return null;
		}

		/// <summary>
		/// Determines whether the library is available.
		/// </summary>
		/// <returns>Whether the library is available.</returns>
		public static bool GetLibraryLocationStatus()
		{
			Contract.Assume(Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList != null);

			return Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.ContainsItem("LibraryFolderToken");
		}

		/// <summary>
		/// Retrieves the library location.
		/// </summary>
		/// <returns>The library location.</returns>
		public async static Task<Windows.Storage.StorageFolder> GetLibraryLocation()
		{
			Contract.Assume(Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList != null);

			try
			{
				return await Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.GetFolderAsync("LibraryFolderToken");
			}
			catch (Exception)
			{
				return null;
			}
		}

		/// <summary>
		/// Sets the library location.
		/// </summary>
		/// <param name="cache">The new library location.</param>
		public static void SetLibraryLocation(Windows.Storage.IStorageFolder cache)
		{
			Contract.Assume(Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList != null);

			if (cache == null)
			{
				if (GetLibraryLocationStatus()) Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.Remove("LibraryFolderToken");
			}
			else
			{
				Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("LibraryFolderToken", cache);
			}
		}

		/// <summary>
		/// Gets an item from the artwork cache.
		/// </summary>
		/// <param name="trackID">The item's ID.</param>
		/// <returns>A CacheReceipt that indicates the success of the request and the Uri of the image, if possible.</returns>
		internal static CacheReceipt<Uri> GetArtworkCacheItem(uint trackID)
		{
			var x = _local.CreateContainer(ArtContainerGuid.ToString(), Windows.Storage.ApplicationDataCreateDisposition.Always).Values[trackID.ToString()] as string;

			if (x == null)
			{
				return new CacheReceipt<Uri>(CacheStatus.Uncached, null);
			}
			else if (x.Length == 0)
			{
				return new CacheReceipt<Uri>(CacheStatus.CannotCache, null);
			}
			else
			{
				return new CacheReceipt<Uri>(CacheStatus.Cached, new Uri(x));
			}
		}

		/// <summary>
		/// Sets an item in the artwork cache.
		/// </summary>
		/// <param name="artGuid">The ArtContainerGuid at the start of the cache operation. If it is different from the current ArtContainerGuid, the cache will be ignored.</param>
		/// <param name="status">The status of the cache operation.</param>
		/// <param name="trackID">The item's ID.</param>
		/// <param name="path">The path to the image, if status is CacheStatus.Cached.</param>
		/// <returns>Whether the cache was successful.</returns>
		internal static bool SetArtworkCacheItem(Guid artGuid, CacheStatus status, uint trackID, Uri path)
		{
			if (artGuid == ArtContainerGuid)
			{
				switch (status)
				{
					case CacheStatus.Cached:
						_local.CreateContainer(ArtContainerGuid.ToString(), Windows.Storage.ApplicationDataCreateDisposition.Always).Values[trackID.ToString()] = path.ToString();
						break;

					case CacheStatus.CannotCache:
						_local.CreateContainer(ArtContainerGuid.ToString(), Windows.Storage.ApplicationDataCreateDisposition.Always).Values[trackID.ToString()] = string.Empty;
						break;

					case CacheStatus.Uncached:
						break;
				}
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Deletes any album artwork containers that are not the current one in the local storage.
		/// API consumers should periodically call this function during normal app operation.
		/// </summary>
		public static async void CleanArtworkCache()
		{
			try
			{
				string artFolder = ArtContainerGuid.ToString();

				var subfolders = await Windows.Storage.ApplicationData.Current.LocalFolder.GetFoldersAsync();
				foreach (var folder in subfolders)
				{
					if (folder.Name != artFolder)
					{
						await folder.DeleteAsync();
					}
				}
			}
			catch (Exception)
			{
				// Justification: artwork cache cleaning happens automatically, and failure does not harm user
			}
		}
	}
}