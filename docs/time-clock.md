# Staff PIN, clock-in and worked hours

Module: `own_pos`. Built for the cashiers at the Yurt POS till, who share one tablet during a shift.

## The staff PIN
- Every staff member can have a **staff PIN**: 4 to 6 digits, stored hashed. It is **not** the manager PIN (see `docs/roles.md`), and it is different from every other staff PIN and every manager PIN of the cafe, so one number always means one person. Setting a PIN that already exists is refused ("Someone else already uses this PIN").
- **Own PIN:** admin app → *Settings → Staff PIN*, confirmed with the account password. Workers see this page too.
- **Set by a manager:** on the same page an **Admin** sets or removes the PIN of Workers, the **CEO** of everybody (new cashier's first PIN, a lost PIN). Audit actions `StaffPinSet`, `StaffPinCleared`.
- **Guessing:** 5 wrong PINs in a row lock the signed-in till login for 5 minutes (`StaffPinRejected` in the audit log).

## At the till (Yurt POS, "Staff PIN" button)
1. **Take over the till:** the cashier types their PIN; the till signs in as them (a new login, the previous refresh token is revoked). The sale in progress stays on screen, so a guest does not wait. Sales, tips and the audit log now carry the new name (`StaffSwitched` in the audit log, written under the person who handed over).
   - Only **Workers and Admins** can be switched to. The **CEO** signs in with the password. An account that still has to choose a new password must sign in with its password once.
2. **Clock in / out:** the same PIN toggles the person's working time: first press clocks in, next press clocks out. It works for anybody with a PIN, including the CEO, and does not change who is signed in at the till.

Both actions need the internet because the server checks the PIN. A sale made offline is not blocked: the cashier keeps selling as the person who is signed in and clocks in when the internet is back.

## Clock in from the admin app (everyone)
Baristas, kitchen staff, Admins and the CEO do not need the till. Every signed-in staff member (not the platform operator) gets a **Clock in / Clock out** button at the bottom of the admin sidebar, which also shows "since 09:12" while clocked in. No PIN: the login is the proof. Works on a phone, so a barista can clock in at the door. `GET admin/time-clock/me`, `POST admin/time-clock/me/toggle`.

## Worked hours
- Entries are in admin app → *Reports → Worked hours* (Admin and CEO): hours per person for a period, every entry, who changed it and why.
- **Forgotten clock-out:** a shift still open after 16 hours is a forgotten one. The person's next clock-in closes it with **0 hours** and marks it ("no clock-out"); the till tells them. It counts as a problem on the report until a manager fixes it.
- **Fixing:** a manager sets the real clock-in/out, adds a missed shift or removes an entry. A **reason is required** and goes into the audit log (`TimeEntryChanged`, `TimeEntryAdded`, `TimeEntryRemoved`). A shift can't be longer than 16 hours or in the future.
- Days and months are cafe time (Asia/Almaty), like the profit reports; a shift belongs to the day and month it **started** in.

## Camera check (with Yurt Watch)
When the cafe has both `own_pos` and `staff_watch`, *Reports → Worked hours* has a **Camera check** box for one branch and day. It compares what the Yurt Watch camera sees at the **station zones** with who is clocked in at that branch, minute by minute, and lists stretches where they disagree:
- **Someone worked and nobody was clocked in** (people at a station for 10+ minutes, zero clocked in): a forgotten clock-in, or someone covering without clocking.
- **More people at the stations than clocked in** (15+ minutes).
- **Clocked in but nobody seen at a station** (60+ minutes): only a hint, because people are also in the kitchen or the storeroom.

The camera counts people and **never names them** (no faces, see the Yurt Watch notes), so this says "a person worked from 09:12 to 09:58 with nobody clocked in", not who. Matching a name to a person is up to the manager, who fixes the entry on the same page. Only minutes where the box sent at least 20 s of pictures count, so a camera that was off proves nothing. Clock-ins from the admin app have no branch: they count for the branch with cameras only when the cafe has just one such branch. It is a hint for the manager, not an alert and not a reason to change anyone's pay by itself. `GET admin/time-clock/camera-check?locationId&day` (Admin, CEO).

## Hours in payroll (module `payroll`)
- On *Payroll* the CEO can set an **hourly rate** next to the monthly salary. Leave 0 for no hourly pay.
- A month's sheet shows the person's hours and `hours × rate` in an extra column. **To pay = salary + hourly pay + bonuses − deductions.** The monthly profit report picks the wages up through the same total.
- Only finished, not forgotten entries count. Closing a month stores hours, rate and hourly pay, so later changes of the rate or of old entries do not change a closed month; reopen it to change it.
- Payroll stays CEO only. Admins see hours, not rates or pay.

## API
| Call | Who | What |
|---|---|---|
| `GET admin/staff-pin` | any staff | `{ iHavePin }` |
| `PUT admin/staff-pin` | any staff | `{ currentPassword, pin }` my own PIN |
| `GET admin/staff-pin/people` | Admin, CEO | people I may set a PIN for |
| `PUT / DELETE admin/staff-pin/{id}` | Admin (workers), CEO | `{ pin }` / remove |
| `POST admin/staff-pin/switch` | any staff | `{ pin, refreshToken? }` → same answer as a login |
| `POST admin/time-clock/punch` | any staff | `{ pin, locationId? }` → `{ action: "in" \| "out", ... }` |
| `GET admin/time-clock/me`, `POST admin/time-clock/me/toggle` | any staff | am I clocked in / clock myself in or out, no PIN |
| `GET admin/time-clock/now` | any staff | who is on the clock (API only, no screen) |
| `GET admin/time-clock/hours?from&to&staffId` | Admin, CEO | report (dates `yyyy-MM-dd`, at most 93 days) |
| `POST / PUT / DELETE admin/time-clock/entries` | Admin, CEO | add, fix, remove; `reason` required |
| `PUT admin/payroll/staff/{id}/salary` | CEO | `{ monthlySalary, hourlyRate? }` |

## Demo data
`seed-demo` gives every demo staff member a staff PIN (`2001`, `2002`, ... in the order of the staff list) and two weeks of clocked shifts.

## Not built yet
- Clocking in by QR/NFC card, or only from the cafe's network/location.
- Clocking in by face (needs written consent of every person and a check of the personal-data rules first).
- Camera check per person (needs identifying people) and Telegram alerts for the camera check.
- Overtime and night-rate rules, breaks, a planned schedule against actual hours.
- Attributing a sale made offline to the cashier who rang it up (it goes with whoever is signed in when it uploads).
