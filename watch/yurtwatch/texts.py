"""Message texts. ru is the default; en for testing. (kk not done yet: needs a native check.)"""

TEXTS = {
    "ru": {
        "empty_while_queue": "⚠️ {station}: никого нет уже {minutes} мин, в очереди {guests} гост.{duty}",
        "empty_on_shift": "⚠️ {station}: никого нет уже {minutes} мин во время смены{duty}",
        "late": "⏰ {name} не на месте ({station}) к {at}, смена с {start}",
        "camera_down": "📷 Камера «{camera}» не отвечает {minutes} мин. Эти минуты не считаются против сотрудников.",
        "camera_back": "📷 Камера «{camera}» снова работает.",
        "on_duty": " На смене: {names}.",
        "report_title": "📊 {cafe}, отчёт за {day}",
        "station_line": "{station}: на месте {covered}% рабочего времени, пусто при очереди {ewq} мин, макс. очередь {qmax}, первый раз {first}, последний {last}",
        "no_data": "{station}: нет данных с камеры",
        "staff_title": "Сотрудники:",
        "staff_line": "{mark} {name} ({station}, {start}–{end}): на месте {covered}%, отсутствовал {empty} мин (из них при очереди {ewq} мин), самое долгое {longest} мин{late}{early}{shared}",
        "late_part": ", опоздал на {m} мин",
        "early_part": ", ушёл раньше на {m} мин",
        "shared_part": " (место делил с другим сотрудником)",
        "never_seen": "{mark} {name} ({station}, {start}–{end}): ни разу не замечен на месте",
        "staff_no_data": "⚪ {name} ({station}, {start}–{end}): нет данных с камеры за смену",
        "camera_gap": "Нет данных с камеры: {m} мин (не засчитано сотрудникам).",
        "alerts_count": "Оповещений за день: {n}.",
        "footer": "Камера считает людей в зонах, лиц не распознаёт. Это подсказка, а не доказательство: проверьте, прежде чем принимать меры.",
        "dash": "—",
    },
    "en": {
        "empty_while_queue": "⚠️ {station}: empty for {minutes} min with {guests} guests waiting.{duty}",
        "empty_on_shift": "⚠️ {station}: empty for {minutes} min during a shift.{duty}",
        "late": "⏰ {name} not at {station} by {at}, shift from {start}",
        "camera_down": "📷 Camera \"{camera}\" not answering for {minutes} min. These minutes don't count against staff.",
        "camera_back": "📷 Camera \"{camera}\" is back.",
        "on_duty": " On duty: {names}.",
        "report_title": "📊 {cafe}, report for {day}",
        "station_line": "{station}: covered {covered}% of open hours, empty with a queue {ewq} min, longest queue {qmax}, first seen {first}, last seen {last}",
        "no_data": "{station}: no camera data",
        "staff_title": "Staff:",
        "staff_line": "{mark} {name} ({station}, {start}–{end}): at station {covered}%, away {empty} min ({ewq} min with a queue), longest {longest} min{late}{early}{shared}",
        "late_part": ", late by {m} min",
        "early_part": ", left early by {m} min",
        "shared_part": " (shared the station)",
        "never_seen": "{mark} {name} ({station}, {start}–{end}): never seen at the station",
        "staff_no_data": "⚪ {name} ({station}, {start}–{end}): no camera data for the shift",
        "camera_gap": "No camera data: {m} min (not counted against staff).",
        "alerts_count": "Alerts today: {n}.",
        "footer": "The camera counts people in zones and does not recognise faces. Treat this as a hint, not proof: check before acting.",
        "dash": "-",
    },
}


def t(lang: str, key: str, **kw) -> str:
    table = TEXTS.get(lang, TEXTS["ru"])
    return table[key].format(**kw)
