using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Exiled.API.Features;
using MEC;
using Newtonsoft.Json;
using static RGM.Variables.Variable;

namespace RGM.API.Features
{
    public static class FileManager
    {
        public static string FolderPath => Path.Combine(Paths.Configs, "RGM");

        private static readonly Mutex _fileMutex = new Mutex(false, "Global\\RGM_FileManager_Mutex");

        private static string ResolvePath(string fileName)
        {
            return Path.IsPathRooted(fileName) ? fileName : Path.Combine(FolderPath, fileName);
        }

        public static void CreateFolder()
        {
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);
        }

        public static void WriteFile(string fileName, string content)
        {
            bool acquired = false;
            try
            {
                acquired = _fileMutex.WaitOne(5000);
                if (!acquired)
                {
                    Log.Warn($"[FileManager] File {fileName} is currently busy. Skipping save to prevent corruption.");
                    return;
                }

                string targetFile = ResolvePath(fileName);
                string directory = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string tempFile = targetFile + ".tmp";
                string backupFile = targetFile + ".bak";

                File.WriteAllText(tempFile, content, Encoding.UTF8);

                if (File.Exists(targetFile))
                {
                    File.Copy(targetFile, backupFile, true);
                    File.Replace(tempFile, targetFile, backupFile, true);
                }
                else
                {
                    File.Move(tempFile, targetFile);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[FileManager] Exception while writing file {fileName}: {ex}");
            }
            finally
            {
                if (acquired)
                    _fileMutex.ReleaseMutex();
            }
        }

        public static string ReadFile(string fileName)
        {
            bool acquired = false;
            try
            {
                acquired = _fileMutex.WaitOne(5000);
                if (!acquired)
                {
                    Log.Warn($"[FileManager] File {fileName} is currently busy. Skipping read to prevent corruption.");
                    return string.Empty;
                }

                string targetFile = ResolvePath(fileName);
                if (!File.Exists(targetFile))
                    return string.Empty;

                return File.ReadAllText(targetFile);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[FileManager] Exception while reading file {fileName}: {ex}");
                return string.Empty;
            }
            finally
            {
                if (acquired)
                    _fileMutex.ReleaseMutex();
            }
        }
    }

    public static class UsersManager
    {
        /*
        Exp - 0
        랜덤코인 - 1
        Cash - 2
        보유한 킬 이펙트 - 3
        장착한 킬 이펙트 - 4
        커스텀 닉네임 - 5
        커스텀 인포 - 6
        보유한 커스터마이징 - 7
        보유한 페인트 - 8
        장착한 페인트 - 9
        보유한 칭호 - 10
        장착한 칭호 - 11
        닉네임 - 12
        연동된 디스코드 ID - 13
        연동 코드 - 14
        킬이펙트 랜덤 여부 - 15
        페인트 랜덤 여부 - 16
        칭호 랜덤 여부 - 17
        보유한 아이템 - 18
        보유한 스폰이펙트 - 19
        장착한 스폰이펙트 - 20
        스폰이펙트 랜덤 여부 - 21
        경고 - 22
        방해 금지 활성화 여부 - 23
        보유한 아이콘 - 24
        장착한 아이콘 - 25
        아이콘 랜덤 여부 - 26
        출석 일수 - 27
        최대 연속 출석 일수 - 28
        출석 여부 (오늘) - 29
        현재 연속 출석 일수 - 30
        */

        private static readonly object _usersLock = new object();

        public static string UsersFileName = Path.Combine(Paths.Configs, "RGM/Users.db");
        public static string LegacyUsersFileName = Path.Combine(Paths.Configs, "RGM/Users.txt");
        public static Dictionary<string, List<string>> UsersCache = new Dictionary<string, List<string>>();

        public static string CheckUser(string userId, int num)
        {
            lock (_usersLock)
            {
                if (UsersCache.ContainsKey(userId) && num >= 0 && num < UsersCache[userId].Count)
                    return UsersCache[userId][num];
            }

            return null;
        }

        public static bool AddUser(string userId, List<string> UserInfo)
        {
            UsersCache[userId] = UserInfo;

            return true;
        }

        public static void SaveUsers()
        {
            lock (_usersLock)
            {
                if (!IsUsersFileLoaded)
                    return;

                Timing.RunCoroutine(RefreshDiscordId());

                var text = JsonConvert.SerializeObject(UsersCache, Formatting.None);
                FileManager.WriteFile(UsersFileName, text);
            }
        }

        public static void LoadUsers()
        {
            lock (_usersLock)
            {
                if (IsUsersFileLoaded)
                    return;

                Timing.RunCoroutine(RefreshDiscordId());

                var dbText = FileManager.ReadFile(UsersFileName);
                if (!string.IsNullOrWhiteSpace(dbText))
                {
                    try
                    {
                        var loadedDb = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(dbText);
                        if (loadedDb != null)
                            UsersCache = loadedDb;

                        IsUsersFileLoaded = true;
                        return;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warn($"[UsersManager] Failed to parse Users.db, trying legacy txt. {ex.Message}");
                    }
                }

                var text = FileManager.ReadFile(LegacyUsersFileName);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Dictionary<string, List<string>> loaded = new Dictionary<string, List<string>>();

                    foreach (var line in text.Split('\n'))
                    {
                        var parts = line.TrimEnd('\r').Split(';');

                        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]))
                            continue;

                        loaded[parts[0]] = parts.Skip(1).ToList();
                    }

                    if (loaded.Count > 0)
                    {
                        UsersCache = loaded;
                        FileManager.WriteFile(UsersFileName, JsonConvert.SerializeObject(UsersCache, Formatting.None));
                        Log.Info("[UsersManager] Migrated Users.txt -> Users.db");
                    }
                }

                IsUsersFileLoaded = true;
            }
        }

        public static IEnumerator<float> RefreshDiscordId()
        {
            var validUsers = UsersManager.UsersCache
            .Where(x => x.Value.Count > 13 && x.Value[13] != "0")
            .GroupBy(userData => userData.Value[13])
            .ToDictionary(group => group.Key, group => group.First().Key);

            DiscordIdToUserId = validUsers;

            yield break;
        }
    }
}
