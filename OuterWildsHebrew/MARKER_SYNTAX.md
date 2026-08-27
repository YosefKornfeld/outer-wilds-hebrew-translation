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
