using System;
using System.Text;
using System.Xml;

namespace xfa.Services;

public class GenerateXmlService
{

    public static async Task GenerateXml()
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "    ",
            Encoding = new UTF8Encoding(false),
            Async = true
        };

        string filePath = Path.Combine(Directory.GetCurrentDirectory(), "Output", "d112.xml");

        using (var writer = XmlWriter.Create(filePath, settings))
        {
            await writer.WriteStartDocumentAsync();

            // Nodul principal cu namespace-ul specificat în XSD
            await writer.WriteStartElementAsync(null, "declaratieUnica", "mfp:anaf:dgti:declaratie_unica:declaratie:v7");

            // Atributele obligatorii pentru declaratieUnica
            await writer.WriteAttributeStringAsync(null, "luna_r", null, "1"); // Exemplu: luna 1-12
            await writer.WriteAttributeStringAsync(null, "an_r", null, "2026"); // Exemplu: an
            await writer.WriteAttributeStringAsync(null, "d_rec", null, "1");
            await writer.WriteAttributeStringAsync(null, "tip_rec", null, "2");
            await writer.WriteAttributeStringAsync(null, "nume_declar", null, "Popescu");
            await writer.WriteAttributeStringAsync(null, "prenume_declar", null, "Ion");
            await writer.WriteAttributeStringAsync(null, "functie_declar", null, "Administrator");

            // Secțiunea Angajator
            await writer.WriteStartElementAsync(null, "angajator", null);
            await writer.WriteAttributeStringAsync(null, "cif", null, "12345678");
            await writer.WriteAttributeStringAsync(null, "caen", null, "6201"); // Exemplu CAEN din lista acceptată
            await writer.WriteAttributeStringAsync(null, "den", null, "NUME FIRMA S.R.L.");
            await writer.WriteAttributeStringAsync(null, "casaAng", null, "B");
            await writer.WriteAttributeStringAsync(null, "datCAM", null, "1");
            await writer.WriteAttributeStringAsync(null, "totalPlata_A", null, "1500");

            // Sub-nod obligatoriu angajatorA (poate fi o buclă dacă ai mai multe rânduri)
            await writer.WriteStartElementAsync(null, "angajatorA", null);
            await writer.WriteAttributeStringAsync(null, "A_codOblig", null, "602");
            await writer.WriteAttributeStringAsync(null, "A_codBugetar", null, "20470101XX");
            await writer.WriteAttributeStringAsync(null, "A_datorat", null, "500");
            await writer.WriteAttributeStringAsync(null, "A_scutit", null, "0");
            await writer.WriteAttributeStringAsync(null, "A_plata", null, "500");
            await writer.WriteEndElementAsync(); // Închide angajatorA

            // Sub-nod obligatoriu angajatorB
            await writer.WriteStartElementAsync(null, "angajatorB", null);
            await writer.WriteAttributeStringAsync(null, "B_cnp", null, "10");
            await writer.WriteAttributeStringAsync(null, "B_sanatate", null, "10");
            await writer.WriteAttributeStringAsync(null, "B_pensie", null, "10");
            await writer.WriteAttributeStringAsync(null, "B_sal", null, "10");
            await writer.WriteAttributeStringAsync(null, "B_brutSalarii", null, "4000");
            await writer.WriteEndElementAsync(); // Închide angajatorB

            await writer.WriteEndElementAsync(); // Închide angajator

            // Secțiunea Asigurați (Poți pune o buclă care citește salariații din baza de date Firebird)
            // foreach (var salariat in listaSalariati) 
            {
                await writer.WriteStartElementAsync(null, "asigurat", null);
                await writer.WriteAttributeStringAsync(null, "cnpAsig", null, "1900101123456");
                await writer.WriteAttributeStringAsync(null, "idAsig", null, "1");
                await writer.WriteAttributeStringAsync(null, "numeAsig", null, "Ionescu");
                await writer.WriteAttributeStringAsync(null, "prenAsig", null, "Vasile");
                await writer.WriteAttributeStringAsync(null, "dataAng", null, "10.10.2026");
                await writer.WriteAttributeStringAsync(null, "asigCI", null, "1");
                await writer.WriteAttributeStringAsync(null, "asigSO", null, "1");
                await writer.WriteAttributeStringAsync(null, "Timp_E3", null, "8");

                // Grupul A sau B pentru asigurat (ex: asiguratA)
                await writer.WriteStartElementAsync(null, "asiguratA", null);
                await writer.WriteAttributeStringAsync(null, "A_1", null, "20");
                await writer.WriteAttributeStringAsync(null, "A_sal1", null, "20");
                await writer.WriteAttributeStringAsync(null, "A_sal2", null, "20");
                await writer.WriteAttributeStringAsync(null, "A_2", null, "0");
                await writer.WriteAttributeStringAsync(null, "A_3", null, "N");
                await writer.WriteAttributeStringAsync(null, "A_4", null, "8");
                await writer.WriteAttributeStringAsync(null, "A_11", null, "4000");
                await writer.WriteAttributeStringAsync(null, "A_12", null, "1000");
                await writer.WriteAttributeStringAsync(null, "A_13", null, "400");
                await writer.WriteAttributeStringAsync(null, "A_14", null, "0");
                await writer.WriteAttributeStringAsync(null, "A_13P", null, "0");
                await writer.WriteAttributeStringAsync(null, "A_14P", null, "0");
                await writer.WriteAttributeStringAsync(null, "A_11P", null, "0");
                await writer.WriteAttributeStringAsync(null, "A_12P", null, "0");
                await writer.WriteAttributeStringAsync(null, "A_14D", null, "0");
                await writer.WriteAttributeStringAsync(null, "A_12D", null, "0");
                await writer.WriteAttributeStringAsync(null, "A_13S", null, "0");
                await writer.WriteAttributeStringAsync(null, "A_13C", null, "0");
                await writer.WriteEndElementAsync(); // Închide asiguratA

                await writer.WriteEndElementAsync(); // Închide asigurat
            }

            await writer.WriteEndElementAsync(); // Închide declaratieUnica
            await writer.WriteEndDocumentAsync();
        }
    }
}
