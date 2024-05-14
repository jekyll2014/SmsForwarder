using Android.App;
using Android.Content;
using Android.OS;

namespace SmsForwarder
{
    public static class ServiceExtension
    {
        public static void StartForegroundServiceCompat<T>(this Context context, Bundle? args = null) where T : Service
        {
            var intent = new Intent(context, typeof(T));
            if (args != null)
            {
                intent.PutExtras(args);
            }

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
        }
    }
}