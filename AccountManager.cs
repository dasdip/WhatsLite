using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace WhatsLite
{
    public class AccountInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Folder { get; set; }
        public bool IsLoggedIn { get; set; }
    }

    public class AccountManager
    {
        private static string ConfigPath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhatsLite");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return Path.Combine(dir, "accounts.json");
            }
        }

        public string ActiveAccountId { get; set; }
        public List<AccountInfo> Accounts { get; set; }

        public AccountManager()
        {
            Accounts = new List<AccountInfo>();
            ActiveAccountId = "default";
        }

        public static AccountManager Load()
        {
            AccountManager mgr = new AccountManager();
            try
            {
                string path = ConfigPath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);

                    // Parse activeAccountId
                    Match mActive = Regex.Match(json, "\"activeAccountId\"\\s*:\\s*\"([^\"]+)\"");
                    if (mActive.Success) mgr.ActiveAccountId = mActive.Groups[1].Value;

                    // Parse accounts array
                    MatchCollection mAccs = Regex.Matches(json, "\\{\\s*\"id\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"name\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"folder\"\\s*:\\s*\"([^\"]+)\"(?:\\s*,\\s*\"isLoggedIn\"\\s*:\\s*(true|false))?\\s*\\}");
                    foreach (Match m in mAccs)
                    {
                        string name = m.Groups[2].Value;
                        if (name.Contains("wordmark") || name.Contains("ic-more-vert") || name.Length > 35)
                        {
                            name = "No Account Logged In";
                        }

                        bool isLog = false;
                        if (m.Groups[4].Success)
                        {
                            isLog = (m.Groups[4].Value == "true");
                        }
                        else
                        {
                            isLog = (!name.Equals("No Account Logged In", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(name));
                        }

                        mgr.Accounts.Add(new AccountInfo {
                            Id = m.Groups[1].Value,
                            Name = name,
                            Folder = m.Groups[3].Value,
                            IsLoggedIn = isLog
                        });
                    }
                }
            }
            catch {}

            // Ensure at least one default account exists (preserving existing UserData)
            if (mgr.Accounts.Count == 0)
            {
                mgr.Accounts.Add(new AccountInfo {
                    Id = "default",
                    Name = "No Account Logged In",
                    Folder = "UserData",
                    IsLoggedIn = false
                });
                mgr.ActiveAccountId = "default";
                mgr.Save();
            }

            // Ensure activeAccountId is valid
            bool found = false;
            foreach (AccountInfo acc in mgr.Accounts)
            {
                if (acc.Id == mgr.ActiveAccountId) { found = true; break; }
            }
            if (!found && mgr.Accounts.Count > 0)
            {
                mgr.ActiveAccountId = mgr.Accounts[0].Id;
            }

            return mgr;
        }

        public void Save()
        {
            try
            {
                List<string> accJsonList = new List<string>();
                foreach (AccountInfo acc in Accounts)
                {
                    accJsonList.Add(string.Format("    {{\"id\": \"{0}\", \"name\": \"{1}\", \"folder\": \"{2}\", \"isLoggedIn\": {3}}}",
                        EscapeJson(acc.Id), EscapeJson(acc.Name), EscapeJson(acc.Folder), acc.IsLoggedIn ? "true" : "false"));
                }

                string json = string.Format(
                    "{{\n  \"activeAccountId\": \"{0}\",\n  \"accounts\": [\n{1}\n  ]\n}}",
                    EscapeJson(ActiveAccountId),
                    string.Join(",\n", accJsonList.ToArray())
                );

                File.WriteAllText(ConfigPath, json);
            }
            catch {}
        }

        public AccountInfo GetActiveAccount()
        {
            foreach (AccountInfo acc in Accounts)
            {
                if (acc.Id == ActiveAccountId) return acc;
            }
            return Accounts.Count > 0 ? Accounts[0] : null;
        }

        public AccountInfo AddAccount(string name = null)
        {
            string id = "acc_" + DateTime.UtcNow.Ticks.ToString();
            string folder = Path.Combine("Profiles", id);

            string initialName = string.IsNullOrEmpty(name) ? "No Account Logged In" : name.Trim();
            AccountInfo newAcc = new AccountInfo {
                Id = id,
                Name = initialName,
                Folder = folder,
                IsLoggedIn = (!initialName.Equals("No Account Logged In", StringComparison.OrdinalIgnoreCase))
            };

            Accounts.Add(newAcc);
            Save();
            return newAcc;
        }

        public void RenameAccount(string id, string newName)
        {
            foreach (AccountInfo acc in Accounts)
            {
                if (acc.Id == id)
                {
                    string trimmed = string.IsNullOrEmpty(newName) ? acc.Name : newName.Trim();
                    acc.Name = trimmed;
                    if (!trimmed.Equals("No Account Logged In", StringComparison.OrdinalIgnoreCase))
                    {
                        acc.IsLoggedIn = true;
                    }
                    Save();
                    break;
                }
            }
        }

        public void ResetAccountToLoggedOut(string id)
        {
            foreach (AccountInfo acc in Accounts)
            {
                if (acc.Id == id)
                {
                    acc.Name = "No Account Logged In";
                    acc.IsLoggedIn = false;
                    Save();
                    break;
                }
            }
        }

        public bool RemoveAccount(string id)
        {
            if (Accounts.Count <= 1) return false; // Must keep at least one account

            AccountInfo target = null;
            foreach (AccountInfo acc in Accounts)
            {
                if (acc.Id == id) { target = acc; break; }
            }

            if (target != null)
            {
                Accounts.Remove(target);
                if (ActiveAccountId == id)
                {
                    ActiveAccountId = Accounts[0].Id;
                }
                Save();

                // Delete profile directory if it's not the primary UserData
                if (!target.Folder.Equals("UserData", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string fullPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "WhatsLite",
                            target.Folder
                        );
                        if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true);
                    }
                    catch {}
                }
                return true;
            }
            return false;
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
