# SmsForwarder

Software to forward incoming SMS messages to Telegram.

Needs Telegram bot API-Key:
 - can be created/managed via the official Telegram BotFather https://t.me/BotFather

and subscriber's ID:
 - can get it at https://t.me/userinfobot

Made for those who have 2 or more SIM cards (different countries, different providers) and only need to get SMS messages on some of them (banc/security SMS codes).

Can be controlled via the Telegram menu/commands.

P.S. '/smssend' command is not properly implemented in the menu for now. can still be run by sending '/smssend <phone_number> <message_text>'

Planned features:
1) Incoming call notification to Telegram
2) PUSH notification forwarding

Known bugs:

1) App needs to be restarted after the Telegram token is changed.
