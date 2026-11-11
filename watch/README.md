# Yurt Watch

A small program for a computer in the cafe. It looks at the cafe's own cameras, counts people in zones you draw
(bar, till, queue) and tells the owner in Telegram when a station is empty while guests wait, when someone is late,
and sends a report after closing, per station and per staff member.

It runs two ways:

- **Connected to Yurt** (module `staff_watch`, recommended): zones, shift plan, alerts and Telegram are set on the
  Staff watch page of the Yurt admin app; the box sends minute numbers there and Yurt sends the Telegram messages.
  See "Connected to Yurt" below.
- **On its own** (the first pilot): everything is set in `config.toml` and `staff.csv`, and the box sends Telegram
  messages itself.

Besides people in zones it can count **visitors** through a door line, spot a **phone in hand** at a station and
notice when **someone does not move** at a station for a long time (sitting still or asleep), even while a colleague
works next to them.

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
python -m venv .venv                      (once; then .venv\Scripts\activate, Linux/Mac: source .venv/bin/activate)
python -m pip install -r requirements.txt
python watch.py download-model --config config.example.toml
copy config.example.toml config.toml      (Linux/Mac: cp config.example.toml config.toml)
copy staff.example.csv staff.csv          (Linux/Mac: cp staff.example.csv staff.csv)
```

Updating from an older box: `python -m pip uninstall -y opencv-python-headless`, then the install line again.

### Better detection: RF-DETR (recommended)

Two detection engines come with the box. YOLOX-Tiny is downloaded above and works everywhere. RF-DETR Nano (newer,
better with people standing close together or half hidden by the coffee machine) has to be made on this computer
once, because its maker publishes it only for PyTorch:

```
python watch.py download-model --engine rfdetr
```

This downloads about 1 GB of tools into `models/.rfdetr-export`, makes `models/rfdetr_nano.onnx` (about 100 MB) in a
few minutes and deletes the tools again. The box then uses RF-DETR by itself (`engine = "auto"` in `config.toml`).
`python watch.py compare` runs both engines on a picture from each camera (or `--video file.mp4`) and saves
side-by-side pictures in `data/compare/`, so you can see which one finds your staff and guests better and how fast.

### Hardware (optional)

`device` in `config.toml` says where the model runs: `cpu`, `intel` (OpenVINO, faster on Intel CPUs and their
built-in graphics: `pip install onnxruntime-openvino`), `nvidia` (an NVIDIA card: `pip uninstall onnxruntime` then
`pip install onnxruntime-gpu`), or `auto` (the best one installed). If the chosen one is missing, the box says so
in the log and uses the CPU.

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

## Trying it without an IP camera

- **A webcam plugged into the computer** (or the laptop's own camera): `url = "0"` (`"1"` for a second one).
- **A home camera that streams over the network** (RTSP, like Hikvision, Dahua, TP-Link Tapo, Imou, EZVIZ):
  use its RTSP address, e.g. `rtsp://user:password@192.168.1.50:554/stream1`. The address is in the camera's
  manual or app (Tapo: "Camera account" in the app; EZVIZ / Imou: turn on RTSP or "local access" first). Cloud-only
  cameras without RTSP (for example most Xiaomi) cannot be read.
- **A video file**: put its path as `url`; it plays in a loop at its real speed.

Point it at a room with people moving, draw a "station" zone around a chair or desk and a "door" line across a
doorway, and you can see visitors, phone in hand and "not moving" alerts on the Staff watch page.

## Connected to Yurt

1. An admin of the cafe switches on `staff_watch` (Platform > Modules) and opens **Staff watch** in the admin app.
2. **Camera box** tab: press "Make key". Copy the `[yurt]` lines it shows into `config.toml` on the box. Keep only
   `[[cameras]]` (name, address, `fps`) and `[detect]` there; zones, shifts and alerts now come from Yurt.
3. `python watch.py connect` checks the key and shows what Yurt sends. Then start `python watch.py run` (see
   "6. Run it"). Within a minute the box tells Yurt its cameras and sends a still picture of each (heads blurred).
4. **Cameras and zones** tab: draw zones on each picture. Station = where a barista or cashier stands, queue = where
   guests wait, door = a line across the doorway plus a point on the inside (visitors are people who cross it inwards).
   The box picks up new zones within a minute; "Take a new picture" asks it for a fresh one.
5. **Shift plan** (optional) and **Settings**: opening hours, alert minutes, phone / stillness switches, the cafe's
   own Telegram bot and the chat of each branch ("Find chat").
6. The Day tab shows visitors vs orders per hour, minutes per station and alerts; the day report arrives after
   closing.

Good to know when connected:

- Phone in hand is found best on the **main stream** (`Channels/101`): the sub stream is small and a phone is a few
  pixels. A phone at the ear, under the counter or behind the back is often missed. Treat it as a hint.
- "Not moving" follows each person at the station on their own: it alerts when one of them has not moved for N
  minutes, even if others there keep working. It cannot tell sleep from a person standing very still while
  waiting; the check picture tells.
- A door camera needs about `fps = 5` so people are seen on both sides of the line. Visitors include couriers and
  staff walking in; compare with orders, not with an exact guest count.
- The box keeps working when the internet drops and sends the missed minutes and alerts later.
- **Right now** on the Day tab: is each station staffed or away (and for how long), how many guests wait, visitors
  in and out today, and whether the box is online (it checks in every minute; offline after 3 missed minutes).
- **Guests waiting with nobody at the station** is checked every second: after `away_grace_seconds` (20) without
  anyone at the station it counts as away, and when guests wait that long in total (`queue_wait_seconds`, 90) an
  alert goes out at once with a small picture (heads blurred) to Telegram and the Day tab.
- To keep the server bill small the box sends minute counts once a minute, the "right now" state only when it
  changes (at most every 10 seconds, never saved up while offline), and pictures only with that one alert.
- Only numbers, alert texts, the day report and a still picture on request leave the cafe. Camera passwords never do.

## 6. Run it

```
python watch.py run
```

To watch what it sees, run `python watch.py run --show`: one window per camera with the zones, a box and foot
point on each person and the state of each station (active / away with a timer / guests waiting with nobody there),
queue and door. Heads are blurred and nothing extra is saved or sent. `q` or Esc closes the windows; watching goes
on. Each window costs a little CPU, so leave it off in normal use.

Keep it running all day. Windows: put a `watch.bat` with `cd C:\path\to\watch` and `python watch.py run` into the
Startup folder (`Win+R`, `shell:startup`). Linux: a systemd service with `Restart=always`, like the print agent.

## What you get

- **Live alerts:** guests waiting with nobody at the station for 90 s (counted by the second, with a picture) ·
  station empty 10 min during someone's shift ·
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
- Detection takes about 50 ms (YOLOX) to 100–200 ms (RF-DETR) per picture on a normal CPU, so 1–4 cameras at one
  picture a second is light work; a door camera at 5 pictures a second costs the most.
- Models: RF-DETR Nano (Roboflow) and YOLOX-Tiny (Megvii), both Apache-2.0, free for commercial use. People are
  followed between pictures with ByteTrack (`trackers`, Apache-2.0) and door lines / zones are checked with
  `supervision` (MIT). Each person is counted at most once each way through a door.
- Run the tests: `python -m unittest discover -s tests -t .`
