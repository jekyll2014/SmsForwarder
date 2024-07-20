using System.Collections.Generic;
using System.Linq;

using Xamarin.Essentials;

namespace SmsForwarder
{
    internal static class AppSettings
    {
        private const string TelegramTokenSettingKey = "TelegramToken";
        public static string TelegramToken
        {
            get => Preferences.Get(TelegramTokenSettingKey, string.Empty);
            set => Preferences.Set(TelegramTokenSettingKey, value);
        }

        private const string AuthorisedUsersSettingKey = "AuthorisedUsers";
        public static long[] AuthorisedUsers
        {
            get
            {
                var users = Preferences.Get(AuthorisedUsersSettingKey, string.Empty)
                    .Split(';');

                var result = new List<long>();
                foreach (var user in users)
                {
                    if (long.TryParse(user, out var u))
                        result.Add(u);
                }

                return result.ToArray();
            }
            set
            {
                var settingValue = value.Aggregate(string.Empty, (current, i) => current + $"{i};");
                Preferences.Set(AuthorisedUsersSettingKey, settingValue);
            }
        }
        public static string AuthorisedUsersString
        {
            get => Preferences.Get(AuthorisedUsersSettingKey, string.Empty);
            set
            {
                var userStrings = value.Split(';');
                var users = new List<long>();
                foreach (var user in userStrings)
                {
                    if (long.TryParse(user, out var u))
                        users.Add(u);
                }

                var settingValue = users.Aggregate(string.Empty, (current, i) => current + $"{i};");
                Preferences.Set(AuthorisedUsersSettingKey, settingValue);
            }
        }

        private const string IgnoredPhonesSettingKey = "IgnoredPhones";
        public static string[] IgnoredPhones
        {
            get
            {
                return Preferences.Get(IgnoredPhonesSettingKey, string.Empty)
                    .Split(';');
            }
            set
            {
                var settingValue = value.Aggregate(string.Empty, (current, i) => current + $"{i.Trim()};");
                Preferences.Set(IgnoredPhonesSettingKey, settingValue);
            }
        }
        public static string IgnoredPhonesString
        {
            get => Preferences.Get(IgnoredPhonesSettingKey, string.Empty);
            set
            {
                var phoneStrings = value.Split(';');
                var settingValue = phoneStrings.Aggregate(string.Empty, (current, i) => current + $"{i.Trim()};");
                Preferences.Set(IgnoredPhonesSettingKey, settingValue);
            }
        }

        public static string RestartOnBootSettingKey = "RestartOnBoot";
        public static bool RestartOnBoot
        {
            get
            {
                var value = Preferences.Get(RestartOnBootSettingKey, string.Empty);
                if (bool.TryParse(value, out var result))
                    return result;

                return false;
            }
            set => Preferences.Set(RestartOnBootSettingKey, value.ToString());
        }
    }
}