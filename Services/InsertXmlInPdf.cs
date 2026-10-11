using System.IO;
using System.Xml;
using iTextSharp.text.pdf;

namespace xfa.Services
{
    /// <summary>
    /// Prefills the XFA form in D112.pdf with data from the XML declaration.
    ///
    /// The D112 form contains an Adobe Reader usage rights signature (/Perms /UR3).
    /// This only allows modifying the form DATA, not the XFA template itself.
    /// Therefore, we write the values into the /datasets section (under nodes with the same
    /// name as the fields' SOM, following the iText/Adobe convention), writing only that
    /// section as an incremental update (append mode). As a result, the template and
    /// signature remain intact, leaving the form editable and saveable even in free Adobe Reader.
    /// </summary>
    public class InsertXmlInPdf
    {
        public static void Insert(string xmlPath, string outputPath)
        {
            var pdfPath = Path.Combine(Directory.GetCurrentDirectory(), "Anaf", "D112.pdf");
            if (!File.Exists(pdfPath))
                throw new FileNotFoundException("Nu am găsit pdf-ul la " + pdfPath);

            if (!File.Exists(xmlPath))
                throw new FileNotFoundException("Nu am găsit xml-ul la " + xmlPath);

            var data = new XmlDocument { PreserveWhitespace = true };
            data.Load(xmlPath);
            var dataNs = data.DocumentElement!.NamespaceURI;

            PdfReader reader = new PdfReader(pdfPath);

            using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                // Mod append: biții originali (inclusiv semnătura ANAF) rămân neatinși;
                // adăugăm doar datele ca actualizare incrementală.
                PdfStamper stamper = new PdfStamper(reader, fs, '\0', true);

                var xfa = stamper.AcroFields.Xfa;
                if (xfa.XfaPresent)
                {
                    FillDatasets(xfa, data, dataNs);

                    // Scriem DOAR partea /datasets, NU și șablonul.
                    WriteDatasets(reader, stamper.Writer, xfa);

                    // SetNodeText setează Xfa.Changed = true; dacă îl lăsăm așa, la
                    // Close() stamper-ul ar rescrie și șablonul (ar rupe semnătura).
                    xfa.Changed = false;
                }

                stamper.Close();
            }
            reader.Close();
        }

        private static void FillDatasets(XfaForm xfa, XmlDocument data, string dataNs)
        {
            var root = data.DocumentElement!;
            var dnsm = new XmlNamespaceManager(data.NameTable);
            dnsm.AddNamespace("d", dataNs);

            void Set(string som, string? value)
            {
                if (string.IsNullOrEmpty(value)) return;
                var node = xfa.DatasetsSom.InsertNode(xfa.DatasetsNode, som);
                xfa.SetNodeText(node, value);
            }

            // ---- Antet declarație ----
            Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmIdentif", "luna_r"), Attr(root, "luna_r"));
            Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmIdentif", "an_r"), Attr(root, "an_r"));
            Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmIdentif", "d_rec"), Attr(root, "d_rec"));
            Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmIdentif", "tip_rec"), Attr(root, "tip_rec"));
            Set(Som("frmMAIN", "sbfrmPage1Ang", "sbfrmFooter", "nume_declar"), Attr(root, "nume_declar"));
            Set(Som("frmMAIN", "sbfrmPage1Ang", "sbfrmFooter", "prenume_declar"), Attr(root, "prenume_declar"));
            Set(Som("frmMAIN", "sbfrmPage1Ang", "sbfrmFooter", "functie_declar"), Attr(root, "functie_declar"));

            // ---- Angajator ----
            if (root.SelectSingleNode("d:angajator", dnsm) is XmlElement ang)
            {
                Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmIdentif", "cif"), Attr(ang, "cif"));
                Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmIdentif", "caen"), Attr(ang, "caen"));
                Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmIdentif", "den"), Attr(ang, "den"));
                Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmIdentif2", "casaAng"), Attr(ang, "casaAng"));
                Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmIdentif2", "datCAM"), Attr(ang, "datCAM"));
                Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmSectATotal", "totalPlata_A"), Attr(ang, "totalPlata_A"));

                if (ang.SelectSingleNode("d:angajatorA", dnsm) is XmlElement angA)
                {
                    Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmSectAVal", "A_codOblig"), Attr(angA, "A_codOblig"));
                    Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmSectAVal", "codbuget"), Attr(angA, "A_codBugetar"));
                    Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmSectAVal", "a_datorat"), Attr(angA, "A_datorat"));
                    Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmSectAVal", "a_deductibil"), Attr(angA, "A_deductibil"));
                    Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmSectAVal", "a_scutit"), Attr(angA, "A_scutit"));
                    Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmSectAVal", "a_plata"), Attr(angA, "A_plata"));
                }

                if (ang.SelectSingleNode("d:angajatorB", dnsm) is XmlElement angB)
                {
                    Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmSectB", "B_cnp"), Attr(angB, "B_cnp"));
                    Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmSectB", "B_sanatate"), Attr(angB, "B_sanatate"));
                    Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmSectB", "B_pensie"), Attr(angB, "B_pensie"));
                    Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmSectB", "B_sal"), Attr(angB, "B_sal"));
                    Set(Som("frmMAIN", "sbfrmPage1Ang", "sfmSectB", "B1_brut_salarii"), Attr(angB, "B_brutSalarii"));
                }
            }

            // ---- Asigurat (prima instanță) ----
            if (root.SelectSingleNode("d:asigurat", dnsm) is XmlElement asig)
            {
                Set(Som("frmMAIN", "sbfrmPage1Asig", "sfmDateIdentif", "cnp_asig"), Attr(asig, "cnpAsig"));
                Set(Som("frmMAIN", "sbfrmPage1Asig", "sfmDateIdentif", "idAsig"), Attr(asig, "idAsig"));
                Set(Som("frmMAIN", "sbfrmPage1Asig", "sfmDateIdentif", "Nume_asig"), Attr(asig, "numeAsig"));
                Set(Som("frmMAIN", "sbfrmPage1Asig", "sfmDateIdentif", "Pren_asig"), Attr(asig, "prenAsig"));
                Set(Som("frmMAIN", "sbfrmPage1Asig", "sfmDateIdentif", "Data_ang"), Attr(asig, "dataAng"));
                Set(Som("frmMAIN", "sbfrmPage1Asig", "sfmDateIdentif", "Asig_ci"), Attr(asig, "asigCI"));
                Set(Som("frmMAIN", "sbfrmPage1Asig", "sfmDateIdentif", "Asig_so"), Attr(asig, "asigSO"));
                Set(Som("frmMAIN", "sbfrmPage1Asig", "sbfrmSectiuneaE", "sbfrmSectiuneaE4_c", "Timp"), Attr(asig, "Timp_E3"));

                if (asig.SelectSingleNode("d:asiguratA", dnsm) is XmlElement asigA)
                {
                    foreach (XmlAttribute a in asigA.Attributes)
                        Set(Som("frmMAIN", "sbfrmPage1Asig", "sbfrmSectiuneaA", a.Name), a.Value);
                }
            }
        }

        /// <summary>
        /// Scrie înapoi în PDF doar fluxul /datasets (datele de formular), ca
        /// actualizare incrementală, lăsând șablonul și semnătura neatinse.
        /// </summary>
        private static void WriteDatasets(PdfReader reader, PdfWriter writer, XfaForm xfa)
        {
            var afObj = reader.Catalog.Get(PdfName.Acroform);
            var af = PdfReader.GetPdfObject(afObj) as PdfDictionary;
            if (af == null) return;
            var afRef = afObj as PdfIndirectReference;

            var xfaObj = af.Get(PdfName.Xfa);
            var xfaRef = xfaObj as PdfIndirectReference;
            var xfaArr = PdfReader.GetPdfObject(xfaObj) as PdfArray;
            if (xfaArr == null) return;

            var idx = -1;
            for (var i = 0; i + 1 < xfaArr.Size; i += 2)
            {
                if (xfaArr[i] is PdfString s && s.ToUnicodeString() == "datasets")
                {
                    idx = i + 1;
                    break;
                }
            }

            if (idx < 0) return;

            var data = XfaForm.SerializeDoc(xfa.DatasetsNode);
            var stream = new PdfStream(data);
            stream.FlateCompress(writer.CompressionLevel);
            var newRef = writer.AddToBody(stream).IndirectReference;
            xfaArr[idx] = newRef;

            // Rescrie tabloul XFA (dacă e obiect indirect) și dicționarul AcroForm
            // (dacă tabloul XFA e direct), ca actualizare incrementală.
            if (xfaRef != null)
                writer.AddToBody(xfaArr, xfaRef);

            if (afRef != null)
                writer.AddToBody(af, afRef);
        }

        private static string Som(params string[] parts)
            => string.Join(".", System.Linq.Enumerable.Select(parts, p => p + "[0]"));

        private static string? Attr(XmlElement element, string name)
            => element.HasAttribute(name) ? element.GetAttribute(name) : null;
    }
}
