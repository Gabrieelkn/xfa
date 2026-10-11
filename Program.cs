using xfa;
using xfa.Services;

await GenerateXmlService.GenerateXml();

string xmlCurrentPath = Path.Combine(Directory.GetCurrentDirectory(), "Output", "d112.xml");
string pdfFinalPath = Path.Combine(Directory.GetCurrentDirectory(), "Output", "D112_final.pdf");

InsertXmlInPdf.Insert(xmlCurrentPath, pdfFinalPath);
