# D112 XFA PDF — Prefill Process Notes

Notes on programmatically pre-filling the Romanian **D112** (declarație unică) PDF
form from an XML declaration, while keeping the form **editable and saveable in
free Adobe Reader** (like Saga Soft does).

---

## 1. Objective

Given:

- `D112.pdf` — the official ANAF form (an Adobe **XFA** form, usage-rights enabled).
- `d112.xml` — the declaration data we generate (`Program.cs`).

Produce `D112_final.pdf` that is:

1. **Pre-filled** with the values from `d112.xml`,
2. Still **editable** (fields + buttons/form JavaScript work),
3. Still **saveable in free Adobe Reader**,
4. Without **corrupting the ANAF signature**.

Stack: .NET 10, C# `net10.0`, `iTextSharp.LGPLv2.Core` **3.8.6**, class
`xfa.InsertXmlInPdf`.

---

## 2. Key facts discovered about `D112.pdf`

Where | What
--- | ---
`/AcroForm` | `/DA /DR /Fields /XFA /NeedAppearances /SigFlags`
`/AcroForm/Fields` | **empty** (`0`) → there are **no AcroForm fields**
`/AcroForm/SigFlags` | `2` (AppendOnly)
`/AcroForm/NeedAppearances` | `false`
`/XFA` | array of 8 parts: `preamble, config, template, localeSet, datasets, xmpmeta, xfdf, postamble`
`config` | `<dynamicRender>required</dynamicRender>`, `adjustData=1`
`template` | **685 fields**, 74 button UI nodes, 434 event scripts
`bind` elements | 82 total: 77 × `match="none"`, 5 × date-picture only → **no data binding**
`datasets` | `<xfa:data xfa:dataNode="dataGroup"/>` → **empty**, `dataGroup` marker
data model | **none** (`mergeroot` / `dataDescription` / `record` absent)
catalog | `/Perms /UR3` → **Adobe Reader usage-rights signature**; `/NeedsRendering true`

Conclusion: this is a **standalone dynamic XFA form** (fields are not data-bound),
created by Adobe LiveCycle, and **Reader-enabled** (the `/UR3` usage-rights
signature) by ANAF so free Reader can save it.

Unrelated file found in the folder: `structura_D112_0726_070926.pdf` is a
92-page **non-XFA** document (instructions), not a form.

---

## 3. Problem timeline (what we tried and what broke)

### 3.1 Compile error at line 39
`stream.Flavors = PdfStream.BEST_COMPRESSION;`
- `PdfStream` in this library has **no `Flavors` property**.
- Fix: `stream.FlateCompress(PdfStream.BEST_COMPRESSION);`
  (compression is a method; constants `BEST_COMPRESSION = 9`, `BEST_SPEED = 1`,
  `DEFAULT_COMPRESSION = -1` live on `PdfStream`).

### 3.2 "The generated XML is not embedded"
- The XML *was* attached as an embedded file (`/Names/EmbeddedFiles`), but that is
  **not** the form's data.
- The PDF's own `DecUnica.xml` is produced by a **JavaScript export routine**
  inside the XFA template (it reads field `rawValue`s and builds the
  `declaratieUnica` XML). Extracting/saving data from the PDF therefore never
  yields our file.
- Mismatch noted: `d112.xml` (schema shape: `angajator` + `asigurat`) vs
  `DecUnica.xml` (all sections under `angajator`). Neither validates against
  `d112_schema.xsd` (e.g. `casaAng="B"` is not in the enum; `asigurat` is missing
  required `asiguratC/E1/E2/E3`).

### 3.3 Attempt A — fill the XFA **template** (rewrite whole PDF, no append)
Result: data showed, but the PDF became **read-only**, buttons dead.
Cause: a full rewrite rearranges the file and **invalidates the `/Perms/UR3`
usage-rights signature**; Reader then locks the form.

### 3.4 Attempt B — fill the template in **append mode**
Result: still read-only / buttons dead.
Cause: the signature stays in the file, but editing the **template** (the form
*definition*) is **outside what the usage-rights signature allows**, so Reader
still locks the form.

### 3.5 Attempt C — remove the usage rights (`reader.RemoveUsageRights()`)
Result: `/Perms` gone, fields/buttons intact, but free Reader can no longer
**save** a filled copy (that right came from the signature). Not what Saga does.

### 3.6 What Saga-style tools actually do
They keep the ANAF signature valid by writing the values into the XFA **datasets**
(the form *data*), not the template. Filling data is an **allowed** change, so the
signature holds and free Reader can fill **and** save.

---

## 4. Root cause (one sentence)

The D112 form is XFA with **no data binding**, so we tried to pre-fill by editing
the **form definition** (template) — but that breaks ANAF's Adobe **usage-rights
signature**, which is what free Reader needs to keep the form editable/saveable.
The correct place to write is the **datasets**, as an **incremental update**.

---

## 5. Final solution

`InsertXmlInPdf.Insert(xmlPath, outputPath)`:

1. Read the XML declaration.
2. Open `D112.pdf` with `PdfReader`.
3. Open `PdfStamper` in **append mode**: `new PdfStamper(reader, fs, '\0', true)`.
   → original bytes (and the signature) stay intact.
4. Get the XFA form from the stamper: `var xfa = stamper.AcroFields.Xfa;`
5. For each value, create the dataset node from the field **SOM path** with
   `xfa.DatasetsSom.InsertNode(xfa.DatasetsNode, som)` then `xfa.SetNodeText(node, value)`.
   Example SOM: `frmMAIN[0].sbfrmPage1Ang[0].sfmIdentif[0].cif[0]`.
6. Write **only** the `/datasets` stream back as an incremental update (new stream
   added, the `/XFA` array and/or `/AcroForm` dict rewritten), leaving the
   `template` stream byte-identical.
7. Set `xfa.Changed = false` so `PdfStamperImp.Close()` does **not** rewrite the
   template.
8. Close the stamper and reader.

### Why this works
- The `template` (form definition) and the `/UR3` signature are untouched.
- Only form **data** changes → the usage-rights signature stays valid → free
  Adobe Reader can edit **and** save.

### Mapping used (XML attribute → template field / SOM leaf)

Section | XML | XFA field
--- | --- | ---
Header | `luna_r, an_r, d_rec, tip_rec` | `sfmIdentif.*`
Header | `nume_declar, prenume_declar, functie_declar` | `sbfrmFooter.*`
Angajator | `cif, caen, den` | `sfmIdentif.*`
Angajator | `casaAng, datCAM` | `sfmIdentif2.*`
Angajator | `totalPlata_A` | `sfmSectATotal.totalPlata_A`
AngajatorA | `A_codOblig, A_codBugetar→codbuget, A_datorat→a_datorat, a_scutit, a_plata` | `sfmSectAVal.*`
AngajatorB | `B_cnp, B_sanatate, B_pensie, B_sal, B_brutSalarii→B1_brut_salarii` | `sfmSectB.*`
Asigurat | `cnpAsig→cnp_asig, idAsig, numeAsig→Nume_asig, prenAsig→Pren_asig, dataAng→Data_ang, asigCI→Asig_ci, asigSO→Asig_so` | `sbfrmPage1Asig.sfmDateIdentif.*`
Asigurat | `Timp_E3→Timp` | `sbfrmSectiuneaE.sbfrmSectiuneaE4_c.Timp`
AsiguratA | `A_1, A_sal1, …` (names identical) | `sbfrmSectiuneaA.*`

Prefix for all paths: `frmMAIN[0].sbfrmPage1Ang[0]` (or `frmMAIN[0].sbfrmPage1Asig[0]`).

---

## 6. Verification performed

On the produced `D112_final.pdf`:

- `/Perms /UR3` (ANAF usage-rights signature): **present**.
- XFA `template` stream: **SHA-256 identical** to the original.
- First `318 864` bytes: identical to `D112.pdf` (true append / incremental update).
- Datasets: populated; **all 49 SOM keys match** the template field paths.
- Valid 1-page PDF, `XFA present = true`, file grows by only ~3 KB.

Still open: confirm visually in free Adobe Reader that values render, fields are
editable, buttons work, and the form can be saved.

---

## 7. Caveats / TODO

1. **Only the first instance** of `angajatorA` / `asigurat` is filled today. The
   form supports repeated rows (`occur max=41`) and multiple asigurat pages
   (`occur max=9999`); extending requires repeating the data nodes with indices
   `[0], [1], …`.
2. **Data layout confirmation**: the SOM-mirror layout matches iText/Adobe
   convention, but the definitive check is opening in Adobe Reader. If values do
   not render, obtain a PDF filled+saved in Adobe (or prefilled by Saga) and match
   its `datasets` layout exactly.
3. **`d112.xml` is not schema-valid** (`d112_schema.xsd`): `casaAng="B"` is not an
   allowed enum value (looks like it should be `_B` / `AB` / …), and `asigurat`
   lacks required `asiguratC` / `asiguratE1` / `asiguratE2` / `asiguratE3`.
4. **We cannot (re)apply Adobe's usage-rights signature from code.** If a form ever
   needs Reader-enabling, only Adobe Acrobat / AEM Forms can do it
   (Acrobat: *Save As Other → Acrobat Reader Extended PDF → Enable More Tools*).

---

## 8. Key library notes (iTextSharp.LGPLv2.Core 3.8.6)

- `PdfStream` has no `Flavors`; compress with `FlateCompress(int)`.
- `PdfStamper(reader, fs, '\0', true)` = **append mode** (keeps original bytes).
- `PdfStamper.AcroFields.Xfa` is the XFA form owner used on `Close()`; it rewrites
  the template+datasets only when `Xfa.Changed == true`.
- This port's `XfaForm` has **no `FillXfaForm`**; `SetXfa(...)` rewrites **both**
  template and datasets, so for signature-preserving fills we wrote only the
  datasets manually via `PdfWriter.AddToBody`.
- `PdfReader.RemoveUsageRights()` exists (drops `/Perms /UR3`), but removing the
  signature costs free-Reader "save" — not used in the final approach.
