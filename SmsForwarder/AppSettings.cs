using Plugin.Settings;

using System.Collections.Generic;
using System.Linq;

namespace SmsForwarder
{
    internal static class AppSettings
    {
        private const string TelegramTokenSettingKey = "TelegramToken";
        public static string TelegramToken
        {
            get => CrossSettings.Current.GetValueOrDefault(TelegramTokenSettingKey, string.Empty);
            set => CrossSettings.Current.AddOrUpdateValue(TelegramTokenSettingKey, value);
        }


        private const string AuthorisedUsersSettingKey = "AuthorisedUsers";
        public static long[] AuthorisedUsers
        {
            get
            {
                var users = CrossSettings.Current.GetValueOrDefault(AuthorisedUsersSettingKey, string.Empty)
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
                CrossSettings.Current.AddOrUpdateValue(AuthorisedUsersSettingKey, settingValue);
            }
        }
        public static string AuthorisedUsersString
        {
            get => CrossSettings.Current.GetValueOrDefault(AuthorisedUsersSettingKey, string.Empty);
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

                CrossSettings.Current.AddOrUpdateValue(AuthorisedUsersSettingKey, settingValue);
            }
        }
    }
}