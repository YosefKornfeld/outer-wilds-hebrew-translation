# Hebrew Marker Syntax Reference

## Why Markers?

When translating into Hebrew, writing Latin-script rich-text tags like `<color=lightblue>` inside an RTL paragraph means the cursor jumps around in the editor and tags land nowhere near where they look like they should. This is miserable to author.

The marker syntax lets you write the entire translation in Hebrew — tags and all — by wrapping them in a delimiter that never occurs in real Hebrew text: **three final tsadi characters** (`ץץץ`).

```
ץץץצבע כחול בהירץץץאטלרוקץץץצבע סוףץץץ
```

compiles to:

```
<color=lightblue>אטלרוק</color>
```

The whole line stays in right-to-left order in your editor, tags never interrupt the Hebrew run, and the compiler handles the conversion as the game loads.

---

## Marker Structure

A marker is text sandwiched between two runs of three final tsadi:

```
ץץץ [Hebrew text here] ץץץ
```

The Hebrew text inside is **trimmed** and **whitespace-normalized** (any run of spaces collapses to one), so these all compile identically:

```
ץץץצבע כחול בהירץץץ
ץץץ צבע כחול בהיר ץץץ
ץץץ  צבע   כחול   בהיר  ץץץ
```

---

## Vocabulary

### Opening Tags: Element Only

No argument needed — tag opens with just the element name.

| Marker | Compiles to |
|---|---|
| `ץץץנטויץץץ` | `<i>` |
| `ץץץמודגשץץץ` | `<b>` |

### Closing Tags: Element + "סוף"

The word `סוף` (end) means "close this tag."

| Marker | Compiles to |
|---|---|
| `ץץץנטוי סוףץץץ` | `</i>` |
| `ץץץמודגש סוףץץץ` | `</b>` |
| `ץץץצבע סוףץץץ` | `</color>` |
| `ץץץגודל סוףץץץ` | `</size>` |

### Colour Tags: Element + Colour Name

| Marker | Compiles to |
|---|---|
| `ץץץצבע כתוםץץץ` | `<color=orange>` |
| `ץץץצבע כחול בהירץץץ` | `<color=lightblue>` |
| `ץץץצבע אפורץץץ` | `<color=grey>` |
| `ץץץצבע אדוםץץץ` | `<color=red>` |
| `ץץץצבע שחורץץץ` | `<color=black>` |

You can also use hex literals for custom colours:

| Marker | Compiles to |
|---|---|
| `ץץץצבע #808080ffץץץ` | `<color=#808080ff>` |

### Size Tags: Element + Any Number

You pick the size. Any number works, and it can be an integer or a decimal.

| Marker | Compiles to |
|---|---|
| `ץץץגודל 20ץץץ` | `<size=20>` |
| `ץץץגודל 50ץץץ` | `<size=50>` |
| `ץץץגודל 37ץץץ` | `<size=37>` |
| `ץץץגודל 18.5ץץץ` | `<size=18.5>` |

You are **free to use sizes that never appear in the English file**, and to add sizes to Hebrew entries that originally had none. Use whatever the translation needs.

### Pause Tags: Element Alone or Element + Number

A bare pause is a short beat; add a number for longer pauses.

| Marker | Compiles to |
|---|---|
| `ץץץהשהיהץץץ` | `<Pause/>` |
| `ץץץהשהיה 0.5ץץץ` | `<Pause=0.5>` |
| `ץץץהשהיה 1ץץץ` | `<Pause=1>` |
| `ץץץהשהיה 2ץץץ` | `<Pause=2>` |
| `ץץץהשהיה 3ץץץ` | `<Pause=3>` |
| `ץץץהשהיה 1.5ץץץ` | `<Pause=1.5>` |

Like sizes, you can use any number, and you can add pauses where the English had none.

### Substitution Tokens

These are placeholders the game fills in at runtime with actual values. They take no arguments — write them exactly as shown.

| Marker | Compiles to |
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

**Do not drop these.** The game substitutes them at runtime. If a line in English has `<TimeMinutes>`, your Hebrew translation must have `ץץץדקותץץץ` — omitting it is always a bug, even if the line seems to make sense without the number.

---

## Complete Examples

### Simple colour

**English:**
```
The &lt;color=lightblue&gt;Eye&lt;/color&gt; is ancient.
```

**Hebrew:**
```
העין ץץץצבע כחול בהירץץץשל אלפי שנים.
```

compiles to:

```
העין <color=lightblue>של אלפי שנים.
```

### Mixed formatting

**English:**
```
The &lt;i&gt;&lt;color=orange&gt;Ash Twin Project&lt;/color&gt;&lt;/i&gt; is dangerous.
```

**Hebrew:**
```
הפרויקט ץץץנטויץץץץץץצבע כתוםץץץתאום אפרץץץצבע סוףץץץץץץנטוי סוףץץץ מסוכן.
```

compiles to:

```
הפרויקט <i><color=orange>תאום אפר</color></i> מסוכן.
```

### With substitution tokens

**English:**
```
You have &lt;TimeMinutes&gt; minutes remaining.
```

**Hebrew:**
```
נותרו לך ץץץדקות נותרוץץץ דקות.
```

compiles to:

```
נותרו לך <RemainingMinutes> דקות.
```

When the player reads this in game, the engine replaces `<RemainingMinutes>` with the actual number.

### Adding a pause where English had none

**English:**
```
FILIX: We should try something different.
```

**Hebrew:**
```
פיליקס: ץץץהשהיה 2ץץץ אנחנו צריכים לנסות משהו שונה.
```

The Hebrew line gets a two-second pause at the start. The English never had a pause tag; you added one because the Hebrew pacing needs it. This is **correct and expected**.

---

## What Happens When Something Goes Wrong

### Unknown marker

If you typo a colour or misspell a token name:

```
ץץץצבע ורודץץץ
```

(no colour called `ורוד` exists)

The compiler **logs an error** to the OWML console and **passes the marker through untouched**, so it displays in-game as:

```
ץץץצבע ורודץץץ
```

That way you see it immediately and can fix it. Nothing silently disappears.

### Non-numeric size or pause

```
ץץץגודל bigץץץ
```

Similar — logged and passed through:

```
ץץץגודל bigץץץ
```

### Unterminated marker (odd number of markers)

If you open a marker but forget the closing three tsadi:

```
היום היה ץץץנטוי מדהים
```

The compiler logs this and puts the marker back exactly as written:

```
היום היה ץץץנטוי מדהים
```

So you spot it in-game and can close it.

---

## Validator: Automatic Checking

At startup, the mod compares your Hebrew values against their English keys and warns about:

- **Dropped substitution tokens** — if the English has `<TimeMinutes>` and your Hebrew has neither `ץץץדקותץץץ` nor any other time marker, you'll see a warning in the OWML console.
- **Formatting mismatches** — if you wrote `ץץץצבע כחול בהירץץץ` (lightblue) but the English key says `<color=orange>`, that's logged.
- **Unclosed or extra tags** — if the English has two `<i>` but you only closed one, you'll see it.

**Sizes and pauses are not checked.** You can add them, remove them, or retune them at will — the Hebrew's pacing is its own thing.

---

## Tips

1. **Test in-game early.** The OWML console will show compilation errors and validation warnings the instant you load the mod. Don't wait until deep playthrough.

2. **The marker is visually distinct.** `ץ` is rare enough in real Hebrew that even a typo (like four tsadi instead of three) jumps out if you're reading your own text.

3. **Whitespace is forgiving.** Spaces around the element and argument names don't matter:

   ```
   ץץץ צבע כחול בהיר ץץץ    ← same as
   ץץץצבע כחול בהירץץץ        ← this
   ```

4. **Markers can span words.** You don't have to close and reopen for every word:

   ```
   ץץץצבע כחול בהירץץץהעין המסתורית של היקוםץץץצבע סוףץץץ
   ```

   is fine and compiles to:

   ```
   <color=lightblue>העין המסתורית של היקום</color>
   ```

5. **If you need a literal `ץ`** (very rare), it's safe anywhere that's not part of a three-tsadi run. Hebrew does use final tsadi at the end of words like `ארץ`, and those work fine.

---

## Quick Reference

| Action | Example |
|---|---|
| Open italic | `ץץץנטויץץץ` |
| Close italic | `ץץץנטוי סוףץץץ` |
| Set colour | `ץץץצבע כחול בהירץץץ` |
| Close colour | `ץץץצבע סוףץץץ` |
| Set size | `ץץץגודל 25ץץץ` |
| Close size | `ץץץגודל סוףץץץ` |
| Short pause | `ץץץהשהיהץץץ` |
| Long pause | `ץץץהשהיה 2ץץץ` |
| Time remaining | `ץץץדקות נותרוץץץ` |

