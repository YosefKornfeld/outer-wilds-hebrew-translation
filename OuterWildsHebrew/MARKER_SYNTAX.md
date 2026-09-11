# Hebrew Marker Syntax

Write tags in Hebrew instead of `&lt;...&gt;` so the line never breaks RTL flow. Wrapped in three final tsadi: `ץץץ...ץץץ`.

## Line break
`//נ` → new line (splits into independently-reordered paragraphs)

## Tags

| Write | Get |
|---|---|
| `ץץץנטויץץץ` ... `ץץץנטוי סוףץץץ` | `<i>` ... `</i>` |
| `ץץץמודגשץץץ` ... `ץץץמודגש סוףץץץ` | `<b>` ... `</b>` |
| `ץץץצבע כתוםץץץ` ... `ץץץצבע סוףץץץ` | `<color=orange>` ... `</color>` |
| `ץץץצבע כחול בהירץץץ` | `<color=lightblue>` |
| `ץץץצבע אפורץץץ` | `<color=grey>` |
| `ץץץצבע אדוםץץץ` | `<color=red>` |
| `ץץץצבע שחורץץץ` | `<color=black>` |
| `ץץץצבע #rrggbbץץץ` | `<color=#rrggbb>` |
| `ץץץגודל <מספר>ץץץ` ... `ץץץגודל סוףץץץ` | `<size=<מספר>>` ... `</size>` (any number) |
| `ץץץהשהיהץץץ` | `<Pause/>` |
| `ץץץהשהיה <מספר>ץץץ` | `<Pause=<מספר>>` (any number) |

## Substitution tokens (no argument, must match the English key)

| Write | Get |
|---|---|
| `ץץץדקותץץץ` | `<TimeMinutes>` |
| `ץץץשניותץץץ` | `<TimeSeconds>` |
| `ץץץדקות נותרוץץץ` | `<RemainingMinutes>` |
| `ץץץשניות נותרוץץץ` | `<RemainingSeconds>` |
| `ץץץזמן דקות נותרוץץץ` | `<TimeMinutesRemaining>` |
| `ץץץדקות מאז ענק אדוםץץץ` | `<MinutesSinceRedGiant>` |
| `ץץץשניות מאז ענק אדוםץץץ` | `<SecondsSinceRedGiant>` |
| `ץץץדקות עד ענק אדוםץץץ` | `<MinutesToRedGiant>` |
| `ץץץשניות עד ענק אדוםץץץ` | `<SecondsToRedGiant>` |
| `ץץץמספר לולאותץץץ` | `<NbTimeloops>` |
| `ץץץלולאה ראשונהץץץ` | `<FirstLoop>` |
| `ץץץשם פרופילץץץ` | `<Profile Name>` |
| `ץץץסימן קריאהץץץ` | `<!>` |

## Example

```
פיליקס: למזלנו, האטמוספירה של הץץץצבע כחול בהירץץץאטלרוקץץץצבע סוףץץץ לא קיימת//נ נכון שזה ץץץנטויץץץנהדרץץץנטוי סוףץץץ?
```

An unknown or unterminated marker is logged to the OWML console and left visible in-game rather than silently dropped.





# מילון תרגומים
## כוכבי לכת / מקומות

| שם | original |
|---|---|
| `קמין עץ` | `Timber Hearth` |
| `רסיסלע` | `Attlerock` |
| `סבך אפל` | `Dark Bramble` |
| `תאומי שעון החול` | `Hourglass Twins` |
| `תאום אפר` / `תאומאפר` | `Ash Twin` |
| `תאום גחלת` / `תאומאש` | `Amber Twin` |
| `פיר שבריר`| `Brittle Hollow` |

| שם | original |
|---|---|
| `קפסולת מילוט` | `escape pod` |
| `העיר התלויה` | `the hanging city` |


## כלים (Tools)

| שם | original |
|---|---|
| `אותות סקופ` | `Signal Scope` |
| `גששון` | `little scout` |
| `גששגר` | `scout launcher` |
| `חללית` | `ship` |

## מושגים (Concepts)

| שם | original |
|---|---|
| `קמינאים` | `Hearthians` |
| `אבקוע` | `hatchling` |
| `קפצור` | `warp` |
| `קפצור דרך` | `warp travel` |
| `הספינה` | `the vessel` |
| `פרויקט תאום האפר` | `Ash Twin Project` |
| `משואת מצוקה` | `distress beacon` | 