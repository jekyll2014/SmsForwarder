# SmsForwarder

Software to forward incoming SMS messages to Telegram.

Needs Telegram bot API-Key:
 - can be created/managed via official Telegram BotFather https://t.me/BotFather

and subscriber's ID:
 - can get it at https://t.me/userinfobot

Made for those who have 2 or more SIM cards (differenc countries, different providers) and only need to get SMS messages on some of them (banc/security SMS codes).

Can be controlled via Telegram menu/commands.

P.S. Not all the commands are implemented for now.



Known bugs:

1) software needs to be restarted after Telegram token or user ID list is changed.

2) The permssion acces for the PHONE is not working properly. Please check/approve permissions manually in the app properties manually for the first time.