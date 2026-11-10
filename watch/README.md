# Yurt Watch pilot

A small program for a computer in the cafe. It looks at the cafe's own cameras, counts people in zones you draw
(bar, till, queue) and tells the owner in Telegram when a station is empty while guests wait, when someone is late,
and sends a report after closing, per station and per staff member.

It is a 2-week pilot and runs on its own, without the Yurt server. If it pays off, it becomes a Yurt module later.

**What it does not do:** no face recognition, no audio, no video recording. Names come only from the shift list
(`staff.csv`), never from the camera. Every second the newest picture is checked and thrown away. Only minute counts,
reports and a few check pictures (heads blurred, deleted after 7 days) are kept, on this computer.

## 1. What you need

- A computer in the cafe that stays on while the cafe is open, on the same network as the cameras: any Windows PC,
  a mini PC (Intel N100 is enough for 2–4 cameras) or a Linux box. A laptop is fine for the pilot.
- Python 3.11 or newer (https://www.python.org/downloads/; on Windows tick "Add Python to PATH").
- The camera address and a login. On Hikvision, make a separate user with view-only rights
  (camera web page → Configuration → System → User Management), not the admin password.
- A Telegram bot for the messages (step 4).
- **Before you start:** each staff member signs the notice in [`notice-ru.md`](notice-ru.md), and a sign
  "Ведётся видеонаблюдение" hangs at the counter. Have a lawyer read the notice once.

## 2. Install

Copy this `watch` folder to the cafe computer, open a terminal in it and run:

```
python -m pip install -r requirements.txt
python watch.py download-model --config config.example.toml
copy config.example.toml config.toml      (Linux/Mac: cp config.example.toml config.toml)
copy staff.example.csv staff.csv          (Linux/Mac: cp staff.example.csv staff.csv)
```

## 3. Cameras and zones

1. In `config.toml` put the camera address under `[[cameras]]`. For Hikvision:
   `rtsp://USER:PASSWORD@CAMERA-IP:554/Streaming/Channels/102` (102 = the small sub stream, enough for counting).
   The camera IP is on the recorder screen or in the Hik-Connect app. A camera on an NVR channel: `.../Channels/202`,
   `302`, … (channel number + `02`) at the recorder's IP.
2. Run `python watch.py snapshot`. It saves `data/snapshots/<camera>.jpg` with a grid in percent.
3. Draw the zones in `config.toml`: write the corners of each zone in percent, read off the grid.
   - `station` zones (bar, till): only the floor **behind** the counter, where guests never stand. A person counts where
     their **feet** are, so draw the floor area, not the counter top.
   - `queue` zone: the floor in front of the till where guests wait.
   - Never draw zones over toilets, changing rooms or the staff rest area.
   - If one camera can't see everything, add a second `[[cameras]]` and put zones on it.
4. Run `python watch.py check`. Open `data/snapshots/<camera>-check.jpg`: zones in colour, each person with a red dot
   at the feet and the count per zone. Fix the corners until the counts are right. (Send me the snapshot if you
   want me to draw the zones.)

## 4. Telegram

1. In Telegram open @BotFather, send `/newbot`, copy the token into `bot_token`.
2. Send any message to your new bot, then open `https://api.telegram.org/bot<TOKEN>/getUpdates` in a browser
   and copy `"chat":{"id": ...}` into `chat_id`. (For a group: add the bot to the group, write in it, same link.)
3. `python watch.py test-telegram` — a test message arrives.

## 5. Shift list (`staff.csv`)

```
day,name,station,start,end
daily,Айгерим,till,08:00,16:00
mon,Алия,till,16:00,22:00
2026-11-12,Ержан,bar,16:00,22:00
```

`day` is `daily`, a weekday (`mon`…`sun`) or a date. A dated row replaces that person's weekly row on that day.
`station` is a zone name from `config.toml`. You can edit the file while the program runs; it re-reads it every minute.
If two people share one station at the same time, the report says so: the camera can't tell them apart.

## 6. Run it

```
python watch.py run
```

Keep it running all day. Windows: put a `watch.bat` with `cd C:\path\to\watch` and `python watch.py run` into the
Startup folder (`Win+R`, `shell:startup`). Linux: a systemd service with `Restart=always`, like the print agent.

## What you get

- **Live alerts:** station empty 3 min while 2+ guests wait · station empty 10 min during someone's shift ·
  someone not at their station 10 min after the shift starts · a camera not answering (those minutes are never
  counted against staff). Each alert fires once, then waits until the station is covered again. Times are in `[alerts]`.
- **Daily report** 15 min after closing (or after the last shift): per station the % of open hours covered, minutes
  empty while guests waited, longest queue; per person % at their station, minutes away, longest absence,
  late / left early. Also saved in `data/reports/` with a CSV per day for Excel.
- **Check pictures** every 30 min and at each alert in `data/checks/<date>/`, heads blurred. Use them to check the
  counting is right: this is how we measure accuracy during the pilot.

`python watch.py report --date 2026-11-12` rebuilds a day's report; add `--send` to send it again.

## What to check during the pilot

1. First days: look at a few check pictures a day. Are the counts right? Are staff hidden by the coffee machine?
   Move the zone or the camera if needed.
2. For each alert: was it right? Write down wrong ones; we tune the minutes and zones from that.
3. After 2 weeks: did the alerts or reports change anything (fewer empty-till minutes, shorter queues)? That
   decides whether it becomes a paid Yurt module.

## Good to know

- Counting is a hint, not proof. Talk to the person before acting on a number.
- Data: minute counts 90 days, check pictures 7 days, all in `data/` on this computer. Telegram messages with names
  pass through Telegram's servers; ask the lawyer if that is fine with the staff notice.
- Detection takes about 50 ms per picture on a normal CPU, so 1–4 cameras at one picture a second is light work.
- The model is YOLOX-Tiny (Apache-2.0 licence, free for commercial use).
- Run the tests: `python -m unittest discover -s tests -t .`
