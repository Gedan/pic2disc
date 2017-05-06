# pic2disc

This is a shitty little tool to monitor the Picarto APis list of online/streaming users for a subset of names. Whenever one of these names changes state, webhook(s) are triggered in discord to send notifications such.

The config is pretty simple. Run the tool once and it'll generate an example config for you thusly:

```json
{
  "AvailableChannels": {
    "NameOfHook": {
      "DiscordURL": "https://api.discord.com/this_is_your/webhook_address",
      "HookUsername": "Bot Display Name",
      "AvatarURL": "Avatar URL"
    }
  },
  "WatchingUsers": {
    "PicartoUsername": {
      "TargetWebhooks": [
        "NameOfHook"
      ]
    }
  },
  "SecondsBetweenChecks": 60
}
```

Make a list of webhooks with names.
Make a list of picarto users and for each one list the names of the webhooks you want to annouce their status to.
HookUsername and AvatarURL are optional. If they are set they will override the settings in the discord webhook itself, but they aren't required.

I considered making them overridable on a per-user-per-hook basis but ¯\_(ツ)_/¯
