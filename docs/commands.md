# Command reference

Everything FlowCommand can do out of the box. You don't need exact phrases: the AI maps what you say to one of the actions below,
then FlowCommand validates it and checks its risk level before running it. Add your own apps, websites, folders and projects in
`config.json` (see [`config.example.json`](../config.example.json)); nothing here needs code changes.

> This page is generated from the same data the application ships with. It lists what is *built in*; your own configuration can add more.

## Result icons

| Icon | Meaning |
|---|---|
| ✓ | Done, and FlowCommand checked the result (for example a window appeared, or Bluetooth reads back as changed). |
| ! | The request was sent, but Windows doesn't report back, so the result is unverified (media keys, opening websites, some launches). |
| ✗ | Failed or not allowed. The message says why, and nothing else was run. |
| ? | FlowCommand needs a clarification. Answer it by dictating again. |
| ◌ | Dry run: this is what would have happened. |

## Actions

| Action | What it does | Risk | Say something like |
|---|---|---|---|
| `open_application` | Launch an installed application | Low | “Open Chrome”, “launch Spotify”, “open my browser” |
| `close_application` | Ask every window of an app to close normally (it can still ask you to save) | Moderate | “Close Discord” |
| `activate_window` | Bring a running app's window to the front | Low | “Switch to VS Code” |
| `minimize_window` | Minimise an app, or the window you were just in | Low | “Minimise Chrome”, “minimise this” |
| `maximize_window` | Maximise an app, or the window you were just in | Low | “Maximise this window” |
| `restore_window` | Restore a minimised or maximised window | Low | “Restore Notepad” |
| `show_desktop` | Minimise everything to show the desktop | Low | “Show the desktop” |
| `restore_windows` | Undo show desktop | Low | “Bring my windows back” |
| `open_folder` | Open a built-in or configured folder in Explorer | Low | “Open Downloads”, “open the recycle bin” |
| `open_project` | Open a configured project folder in its editor | Low | “Open my FrontAge project” |
| `open_settings` | Open a Windows Settings page | Low | “Open Bluetooth settings” |
| `bluetooth_set` | Turn Bluetooth explicitly on or off, then read the state back | Moderate | “Turn Bluetooth off” |
| `media_control` | Press a media key: play/pause, next, previous, stop, volume up/down, mute | Low | “Pause the music”, “turn it up a lot”, “mute” |
| `take_screenshot` | Save a full screenshot, or open the area selector | Low | “Take a screenshot”, “snip an area” |
| `lock_workstation` | Lock the PC | Moderate | “Lock my computer” |
| `open_website` | Open a website from your configured list | Low | “Open GitHub”, “open Gmail” |
| `search_web` | Search the web in your browser | Low | “Search for best pizza near me” |
| `inspect_port` | Show which process is listening on a TCP port | Moderate | “What's using port 3000?” |
| `kill_process_on_port` | Force-terminate the listener on a TCP port (shows a preview, always asks first) | High | “Kill whatever is on port 3000” |

Moderate actions can optionally require confirmation (`confirmModerateActions`); high-risk actions always do. An action that is not in `enabledActions` can never run.

## Media and volume

`media_control` sends the same keys a keyboard's media buttons send, so it works with any player that responds to them.

| Say | Key sent |
|---|---|
| “Play”, “pause”, “resume” | Play/pause |
| “Next song”, “skip” | Next track |
| “Previous song”, “go back a track” | Previous track |
| “Stop the music” | Stop |
| “Volume up”, “turn it up a lot” | Volume up (5 steps; more for “a lot”, up to 25) |
| “Volume down”, “quieter” | Volume down |
| “Mute”, “unmute” | Mute toggle |

Each volume step is about 2%. Windows doesn't report the resulting level, so these show **!**.

## Screenshots

- **“Take a screenshot”** hides the palette, then saves the whole screen to *Pictures → Screenshots* (the same as pressing Win+PrintScreen). FlowCommand confirms a new file appeared.
- **“Snip an area”** hides the palette and opens the Windows snipping selector.

Double-tap the trigger key to bring the palette back afterwards.

## Websites and web search

“Open *name*” opens a site from the `websites` list; anything else (“search for …”, “look up …”, a site that isn't listed) becomes a web search using `searchUrl`.
Websites open in your Windows default browser, or in the application named by `websiteBrowser`. The AI never supplies a URL: addresses come only from your configuration.

| Say | Opens |
|---|---|
| google | https://www.google.com |
| gmail | https://mail.google.com |
| google drive | https://drive.google.com |
| google docs | https://docs.google.com |
| google sheets | https://sheets.google.com |
| google slides | https://slides.google.com |
| google calendar | https://calendar.google.com |
| google maps | https://maps.google.com |
| google photos | https://photos.google.com |
| google translate | https://translate.google.com |
| google meet | https://meet.google.com |
| ai studio | https://aistudio.google.com |
| google cloud console | https://console.cloud.google.com |
| youtube | https://www.youtube.com |
| youtube music | https://music.youtube.com |
| netflix | https://www.netflix.com |
| prime video | https://www.primevideo.com |
| disney plus | https://www.disneyplus.com |
| twitch | https://www.twitch.tv |
| spotify web | https://open.spotify.com |
| soundcloud | https://soundcloud.com |
| imdb | https://www.imdb.com |
| github | https://github.com |
| gitlab | https://gitlab.com |
| stack overflow | https://stackoverflow.com |
| microsoft learn | https://learn.microsoft.com |
| mdn | https://developer.mozilla.org |
| npm | https://www.npmjs.com |
| nuget | https://www.nuget.org |
| codepen | https://codepen.io |
| vercel | https://vercel.com |
| aws console | https://console.aws.amazon.com |
| azure portal | https://portal.azure.com |
| hugging face | https://huggingface.co |
| kaggle | https://www.kaggle.com |
| hacker news | https://news.ycombinator.com |
| chatgpt | https://chatgpt.com |
| claude | https://claude.ai |
| gemini | https://gemini.google.com |
| perplexity | https://www.perplexity.ai |
| anthropic console | https://console.anthropic.com |
| reddit | https://www.reddit.com |
| x | https://x.com |
| twitter | https://x.com |
| linkedin | https://www.linkedin.com |
| facebook | https://www.facebook.com |
| instagram | https://www.instagram.com |
| tiktok | https://www.tiktok.com |
| pinterest | https://www.pinterest.com |
| whatsapp web | https://web.whatsapp.com |
| telegram web | https://web.telegram.org |
| discord web | https://discord.com/app |
| slack web | https://app.slack.com |
| teams web | https://teams.microsoft.com |
| zoom web | https://zoom.us |
| outlook web | https://outlook.office.com |
| onedrive | https://onedrive.live.com |
| office | https://www.office.com |
| notion web | https://www.notion.so |
| trello | https://trello.com |
| figma web | https://www.figma.com |
| canva | https://www.canva.com |
| dropbox | https://www.dropbox.com |
| wikipedia | https://www.wikipedia.org |
| amazon | https://www.amazon.com |
| ebay | https://www.ebay.com |
| medium | https://medium.com |
| bbc news | https://www.bbc.com/news |
| weather | https://weather.com |

## Applications

65 built-in apps. **Close/switch** means FlowCommand knows the app's process, so it can close, switch to, minimise, maximise or restore its windows.
Apps without it (Store apps, Windows tools, console windows) can still be **opened**. Apps that aren't installed are reported as such; the path list is only a hint, because Start Menu shortcuts and registered App Paths are searched too.

### Browsers

| Say | Id | Also answers to | Close/switch |
|---|---|---|---|
| Google Chrome | `chrome` | — | ✓ |
| Microsoft Edge | `edge` | — | ✓ |
| Firefox | `firefox` | mozilla firefox | ✓ |
| Opera GX | `operagx` | gx | ✓ |
| Opera | `opera` | opera browser | ✓ |
| Brave | `brave` | brave browser | ✓ |
| Vivaldi | `vivaldi` | — | ✓ |

### Editors and IDEs

| Say | Id | Also answers to | Close/switch |
|---|---|---|---|
| Visual Studio Code | `vscode` | code | ✓ |
| Cursor | `cursor` | cursor editor | ✓ |
| Notepad | `notepad` | — | ✓ |
| Notepad++ | `notepadpp` | notepad plus plus | ✓ |
| Sublime Text | `sublime` | — | ✓ |
| Visual Studio | `visualstudio` | — | ✓ |
| IntelliJ IDEA | `intellij` | idea | ✓ |
| PyCharm | `pycharm` | — | ✓ |
| Android Studio | `androidstudio` | — | ✓ |

### Developer tools and terminals

| Say | Id | Also answers to | Close/switch |
|---|---|---|---|
| GitHub Desktop | `githubdesktop` | — | ✓ |
| Postman | `postman` | — | ✓ |
| Docker Desktop | `docker` | — | ✓ |
| Windows Terminal | `terminal` | wt | ✓ |
| Command Prompt | `cmd` | cmd.exe | — |
| PowerShell | `powershell` | windows powershell | — |

### Office, email, notes and PDF

| Say | Id | Also answers to | Close/switch |
|---|---|---|---|
| Microsoft Word | `word` | ms word | ✓ |
| Microsoft Excel | `excel` | ms excel | ✓ |
| Microsoft PowerPoint | `powerpoint` | — | ✓ |
| Microsoft Outlook | `outlook` | — | ✓ |
| OneNote | `onenote` | — | ✓ |
| Thunderbird | `thunderbird` | — | ✓ |
| Notion | `notion` | — | ✓ |
| Obsidian | `obsidian` | — | ✓ |
| Adobe Acrobat | `acrobat` | pdf reader | ✓ |

### Chat and meetings

| Say | Id | Also answers to | Close/switch |
|---|---|---|---|
| Discord | `discord` | — | ✓ |
| Slack | `slack` | — | ✓ |
| Microsoft Teams | `teams` | — | ✓ |
| Zoom | `zoom` | — | ✓ |
| Telegram | `telegram` | — | ✓ |
| WhatsApp | `whatsapp` | — | — |
| Signal | `signal` | — | ✓ |

### Media, recording and creative

| Say | Id | Also answers to | Close/switch |
|---|---|---|---|
| Spotify | `spotify` | — | ✓ |
| VLC | `vlc` | vlc media player | ✓ |
| OBS Studio | `obs` | — | ✓ |
| Audacity | `audacity` | — | ✓ |
| DaVinci Resolve | `davinci` | resolve | ✓ |
| Adobe Photoshop | `photoshop` | — | ✓ |
| Adobe Illustrator | `illustrator` | — | ✓ |
| Adobe Premiere Pro | `premiere` | premiere pro, adobe premiere | ✓ |
| Figma | `figma` | — | ✓ |
| Blender | `blender` | — | ✓ |

### Games

| Say | Id | Also answers to | Close/switch |
|---|---|---|---|
| Steam | `steam` | — | ✓ |
| Epic Games Launcher | `epicgames` | epic | ✓ |

### Windows tools and utilities

| Say | Id | Also answers to | Close/switch |
|---|---|---|---|
| Task Manager | `taskmanager` | taskmgr | ✓ |
| File Explorer | `fileexplorer` | explorer | — |
| Calculator | `calculator` | calc | — |
| Paint | `paint` | mspaint | — |
| Snipping Tool | `snippingtool` | snip | — |
| Control Panel | `controlpanel` | — | — |
| Device Manager | `devicemanager` | — | — |
| Disk Management | `diskmanagement` | — | — |
| Event Viewer | `eventviewer` | — | — |
| Services | `services` | windows services | — |
| Resource Monitor | `resourcemonitor` | resmon | — |
| 7-Zip | `sevenzip` | — | ✓ |
| PowerToys | `powertoys` | — | ✓ |
| Bitwarden | `bitwarden` | — | ✓ |
| TeamViewer | `teamviewer` | — | ✓ |

### Categories (“open my browser”)

`defaultApps` maps a category to an app, so a vague request has an answer. On first run FlowCommand points *browser* at the browser Windows uses.

| Category | Default |
|---|---|
| browser | `chrome` |
| editor | `vscode` |
| terminal | `terminal` |
| email | `outlook` |
| music | `spotify` |
| chat | `discord` |
| video | `vlc` |
| notes | `notepad` |
| office | `word` |

## Folders

Built in: **downloads, documents, desktop, pictures, music, videos, home** (your user folder), **screenshots** (Pictures\Screenshots), **onedrive**, **appdata**, **temp**, **startup** (the Startup folder), **recycle bin** and **this pc**. Add your own under `folders` in `config.json`, for example `"invoices": "%UserProfile%\\Documents\\Invoices"`.

## Windows Settings pages

“Open *page* settings”. FlowCommand can open these pages (and only these):

| Say | Opens |
|---|---|
| bluetooth | `ms-settings:bluetooth` |
| wifi | `ms-settings:network-wifi` |
| network | `ms-settings:network-status` |
| vpn | `ms-settings:network-vpn` |
| airplane mode | `ms-settings:network-airplanemode` |
| sound | `ms-settings:sound` |
| display | `ms-settings:display` |
| night light | `ms-settings:nightlight` |
| notifications | `ms-settings:notifications` |
| power | `ms-settings:powersleep` |
| battery | `ms-settings:batterysaver` |
| storage | `ms-settings:storagesense` |
| apps | `ms-settings:appsfeatures` |
| default apps | `ms-settings:defaultapps` |
| startup apps | `ms-settings:startupapps` |
| mouse | `ms-settings:mousetouchpad` |
| typing | `ms-settings:typing` |
| printers | `ms-settings:printers` |
| privacy | `ms-settings:privacy` |
| microphone privacy | `ms-settings:privacy-microphone` |
| camera privacy | `ms-settings:privacy-webcam` |
| accounts | `ms-settings:yourinfo` |
| date time | `ms-settings:dateandtime` |
| language | `ms-settings:regionlanguage` |
| windows update | `ms-settings:windowsupdate` |
| personalization | `ms-settings:personalization` |
| background | `ms-settings:personalization-background` |
| colors | `ms-settings:colors` |
| lock screen | `ms-settings:lockscreen` |
| taskbar | `ms-settings:taskbar` |
| themes | `ms-settings:themes` |
| about | `ms-settings:about` |
| troubleshoot | `ms-settings:troubleshoot` |
| accessibility | `ms-settings:easeofaccess` |
| clipboard | `ms-settings:clipboard` |
| multitasking | `ms-settings:multitasking` |

Settings pages differ a little between Windows 10 and 11; if a page doesn't exist on your build, Windows opens the Settings home instead.
