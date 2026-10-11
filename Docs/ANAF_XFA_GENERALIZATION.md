# Generalizing ANAF PDF pre-fill (XFA & AcroForm)

How to take the working `D112` solution and reuse it for any ANAF PDF
(D300, D394, D100, D112, D205, …), where the XML is generated from real data.

---

## 1. What is form-specific vs. what is generic

The current `InsertXmlInPdf` mixes both. Split them:

| Form-specific (per PDF/version) | Generic (one engine, all forms) |
| --- | --- |
| XFA field **SOM paths** | Open PDF, append mode, preserve signature |
| XML ↔ field **mapping** (incl. synonyms, case) | Detect AcroForm vs XFA vs hybrid |
| **Repeatable** section structure | Write datasets / AcroForm fields |
| ANAF **XSD** for the XML | XML validation, clear errors |
| Form code + **version/year** | Encryption / password handling |
| — | Repeat instance building (`[0]`, `[1]`, …) |
| — | Logging, unmatched-field report |

Rule of thumb: **the engine never knows about "D112"**; it takes a mapping
descriptor + the PDF + the XML.

---

## 2. Recommended architecture

```
app
 ├─ build XML from DB models        (per form: XSD → classes → serialize)
 ├─ validate XML against ANAF XSD   (per form)
 └─ AnafFormPrefiller.Fill(pdf, xml, mapping)   (generic engine)
        ├─ AcroForm strategy
        └─ XFA datasets strategy (append, signature-safe)
```

### 2.1 Two mapping layers (don't confuse them)

1. **Domain → ANAF XML**: your DB/Firebird model → the declaration XML.
   Best done with classes generated from the ANAF XSD
   (`xsd.exe schema.xsd /classes`), so serialization is schema-correct.
2. **ANAF XML → PDF fields**: the mapping descriptor below.

### 2.2 Mapping descriptor (externalize it)

Put the mapping in a data file per form+version, e.g. `maps/D112.v7.json`:

```json
{
  "form": "D112",
  "version": "v7",
  "strategy": "auto",
  "synonyms": {
    "A_codBugetar": "codbuget",
    "B_brutSalarii": "B1_brut_salarii",
    "cnpAsig": "cnp_asig",
    "numeAsig": "Nume_asig",
    "prenAsig": "Pren_asig",
    "dataAng": "Data_ang",
    "asigCI": "Asig_ci",
    "asigSO": "Asig_so",
    "Timp_E3": "Timp"
  },
  "roots": {
    "angajator": "frmMAIN.sbfrmPage1Ang",
    "asigurat": "frmMAIN.sbfrmPage1Asig"
  },
  "repeats": [
    { "xml": "angajator/angajatorA", "som": "frmMAIN.sbfrmPage1Ang.sfmSectAVal" },
    { "xml": "asigurat/*A", "som": "frmMAIN.sbfrmPage1Asig.sbfrmSectiuneaA" }
  ],
  "overrides": {
    "frmMAIN.sbfrmPage1Ang.sfmSectAVal.a_datorat": "angajator/angajatorA/@A_datorat"
  }
}
```

- `strategy`: `auto` (match by name, use overrides/synonyms), `mapped` (only
  explicit rules), `acroform` (force `AcroFields.SetField`).
- `synonyms`: XML name → XFA leaf name when they differ.
- `repeats`: which XML groups map to which repeating XFA subform.

### 2.3 Auto-mapping (cuts per-form work a lot)

For a new form:

1. Dump the XFA field index from `XfaForm.TemplateSom.Name2Node.Keys`
   (full SOM) — this is the list of targets.
2. Flatten the XML into `leafName → value` and `path → value`
   (ignore namespaces; use local names).
3. Match:
   - exact leaf name → assign,
   - otherwise `synonyms` → assign,
   - otherwise `overrides` → assign.
4. Everything left over: report as **unmatched** so you can add overrides.

In practice most ANAF fields match by name (e.g. `cif`, `caen`, `den`,
`A_codOblig`, `A_1`) and only a handful need synonyms.

### 2.4 Repeatable sections (the main real-world change)

Real data has **many** rows (multiple `angajatorA`, many `asigurat`). The engine
must:

- iterate the repeated XML nodes in order,
- for each index `i`, write the child fields into the XFA repeating subform with
  index `i` (`som[i]`),
- ensure the subform instance exists (dynamic XFA): data node `occur` / repeated
  nodes tell Adobe to instantiate extra subforms.

Our D112 fill currently handles only index `0`. The generic engine loops.

---

## 3. Per-form strategy selection (engine side)

```
read PDF:
  if /AcroForm/Fields not empty        -> AcroForm strategy
  elif /XFA present                    -> XFA datasets strategy
  else                                 -> error "no form fields"

read /Perms:
  if present (usage-rights/docMDP sig) -> MUST append mode; never touch template
  else                                 -> append still recommended; template edits allowed
```

- **AcroForm**: `stamper.AcroFields.SetField(name, value)` (+ `SetField` for
  checkbox/radio using their export values). No datasets fiddling.
- **XFA**: write datasets via SOM (what we did for D112). Use `AcroFields.SetField`
  too if the form is also data-bound/hybrid.
- **Hybrid**: fill both.

---

## 4. Other things ANAF PDFs throw at you

1. **Encrypted forms** (owner password). Handle with
   `new PdfReader(path, ownerPassword)` or `PdfReader.unethicalreading = true`.
   If encrypted + signed, append incremental update and preserve encryption
   (`PdfStamper` with `preserveEncryption` in iText 7; in iText 5 use the right
   constructor).
2. **Version/year changes.** ANAF republishes forms (v6 → v7 → …). Field names and
   SOM paths change. Keep one mapping file **per form+version** and pick by the
   form's `/XFA` namespace or a stored form code.
3. **Signature.** Never modify the template when `/Perms` exists. Prefer datasets.
   You cannot *re-apply* Adobe's usage-rights signature from code (only Acrobat/AEM).
4. **Not all forms are XFA.** Some are plain AcroForm → trivial `SetField`.
5. **Different XML schemas.** Each form has its own XSD; generate classes from it.
   Don't hand-roll XML — validate before filling.
6. **Localization/encoding.** Names with diacritics; keep UTF-8.

---

## 5. Checklist when adding a new ANAF form

1. Put `Form.pdf` + `Form.xsd` in the repo.
2. Generate XML classes: `xsd.exe Form.xsd /classes /namespace:...`.
3. Dump XFA fields (or AcroForm field names) to CSV:
   - XFA: `XfaForm(reader).TemplateSom.Name2Node.Keys`
   - AcroForm: `reader.AcroFields.Fields.Keys`
4. Auto-match XML leaf names ↔ field names; add `synonyms`/`overrides` for the rest.
5. Define `repeats` for repeating sections.
6. Validate a sample: generate XML from real data, fill PDF, open in **Adobe
   Reader** and confirm (a) values render, (b) editable, (c) savable, (d) signature
   still valid ("extended features" not revoked).
7. Store the mapping as `maps/<Form>.<version>.json` and add a regression test.

---

## 6. Real-app data flow (production)

```
DB / domain  ──(mapper)──▶  ANAF XML  ──(validate XSD)──▶  AnafFormPrefiller ──▶  filled PDF
                                   │                              │
                          per-form XSD + classes          per-form mapping JSON
```

- Build the XML from typed models generated from the XSD (guarantees structure).
- Set the correct namespace/version for the form year.
- Validate against the XSD; fail fast with field-level messages.
- Fill the PDF with the generic engine.
- Optionally assert in CI that the output `template` is byte-identical and
  `/Perms` is intact (our D112 checks).

---

## 7. Minimal code change to generalize today

Keep `InsertXmlInPdf.Insert` as a thin façade and extract the engine:

```csharp
public static void Insert(string xmlPath, string outputPath)
    => AnafFormPrefiller.Fill(
           Path.Combine(Directory.GetCurrentDirectory(), "D112.pdf"),
           xmlPath, outputPath,
           FormMapping.Load("maps/D112.v7.json"));

public class AnafFormPrefiller
{
    public static void Fill(string pdfPath, string xmlPath, string outPath, FormMapping map)
    {
        // 1. load + (optional) validate XML
        // 2. open PdfStamper in append mode
        // 3. choose AcroForm vs XFA datasets
        // 4. apply mapping (auto + synonyms + overrides + repeats)
        // 5. write only datasets (XFA) / fields (AcroForm); keep template & /Perms
    }
}
```

The D112 mapping moves out of C# into JSON; the engine becomes reusable for every
form and every version.

---

## 8. Open items specific to this repo

- D112 fill currently handles only the first `angajatorA` / `asigurat` instance.
- `d112.xml` is not schema-valid yet (`casaAng="B"`, missing
  `asiguratC/E1/E2/E3`); fix the XML generator and validate.
- Decide whether the app targets free Reader (keep/require the usage-rights
  signature) or Acrobat (signature optional).
