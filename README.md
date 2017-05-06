# pic2disc

This is a shitty little tool to monitor the Picarto APis list of online/streaming users for a subset of names. Whenever one of these names changes state, webhook(s) are triggered in discord to send notifications such.

The config is pretty simple. Run the tool once and it'll generate an example config. tl;dr

Make a list of webhooks with names.
Make a list of picarto users and for each one list the names of the webhooks you want to annouce their status to.
