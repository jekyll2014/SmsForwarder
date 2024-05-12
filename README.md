# SmsForwarder

Software to forward incoming SMS messages to Telegram.

Needs Telegram bot API-Key:
 - can be created/managed via official Telegram BotFather https://t.me/BotFather

and subscriber's ID:
 - can get it at https://t.me/userinfobot

Made for those who have 2 or more SIM cards (different countries, different providers) and only need to get SMS messages on some of them (banc/security SMS codes).

Can be controlled via Telegram menu/commands.

P.S. Not all the commands are implemented for now.

Planned features:
1) SMS storage access
2) Incoming call notification to Telegram
3) PUSH notification forwarding

Known bugs:

1) software needs to be restarted after the Telegram token or user ID list is changed.

2) The permssion access for the PHONE is not working properly. Please check/approve permissions manually in the app properties for the first time.