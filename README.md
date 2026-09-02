# PDF Toolbox

![Ikona PDF Toolbox / PDF Toolbox icon](src/PdfToolbox.WinForms/Assets/app-icon.png)

[Polski](#polski) · [English](#english)

---

## Polski

### Czym jest PDF Toolbox?

PDF Toolbox to bezpłatna aplikacja desktopowa dla Windows do podstawowej pracy z dokumentami PDF. Została napisana w C# z użyciem .NET 8 i Windows Forms.

Aplikacja działa całkowicie lokalnie i nie wymaga połączenia z Internetem. Dokumenty nie są przesyłane do chmury ani zewnętrznych usług. Program nie zawiera telemetrii, analityki ani internetowego raportowania błędów.

### Dostępne funkcje

- podział jednego PDF na osobny plik dla każdej strony,
- połączenie kilku PDF-ów w jeden dokument z możliwością zmiany kolejności,
- zapisanie tylko wybranych stron,
- utworzenie kopii bez wybranych stron,
- obrót wszystkich lub wybranych stron,
- sekwencyjny podział wielu dokumentów,
- interaktywne wycinanie fragmentów stron PDF do osobnych plików,
- przeciąganie plików metodą drag & drop,
- pasek postępu i bezpieczne anulowanie,
- automatyczne rozwiązywanie konfliktów nazw bez nadpisywania plików,
- ostrzeganie o wykrytych polach podpisu elektronicznego.

Oryginalne dokumenty nigdy nie są modyfikowane ani usuwane.

### Interaktywne wycinanie fragmentów stron PDF

Operacja `Wytnij fragmenty` pozwala:

- wybrać jeden dokument PDF metodą drag & drop lub przez okno wyboru pliku,
- przełączać strony dokumentu,
- oglądać rzeczywisty render strony,
- zaznaczać wiele prostokątnych obszarów,
- przesuwać zaznaczenia, zmieniać ich rozmiar i usuwać je,
- wybrać folder wynikowy,
- zapisać każdy zaznaczony fragment jako osobny PDF.

Nazwy wyników mają postać:

```text
{plik}_strona_{NNN}_fragment_{NN}.pdf
```

Przykład:

```text
Lista_Plac_strona_001_fragment_01.pdf
Lista_Plac_strona_001_fragment_02.pdf
Lista_Plac_strona_003_fragment_01.pdf
```

### Wymagania

Do budowania projektu:

- Windows 10 lub Windows 11 x64,
- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0),
- opcjonalnie Visual Studio 2022 z komponentem „Programowanie aplikacji klasycznych dla platformy .NET”.

Użytkownik gotowej, opublikowanej aplikacji nie potrzebuje Visual Studio, .NET SDK, środowiska .NET ani dodatkowych programów.

### Jak zbudować projekt?

Otwórz PowerShell i przejdź do katalogu projektu:

```powershell
cd C:\Users\Kacper\Desktop\PDFSplitter
```

Następnie wykonaj:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

### Jak opublikować aplikację?

W katalogu głównym projektu wykonaj:

```powershell
dotnet publish src\PdfToolbox.WinForms\PdfToolbox.WinForms.csproj -c Release -r win-x64 --self-contained true
```

Gotowa aplikacja znajdzie się w folderze:

```text
src\PdfToolbox.WinForms\bin\Release\net8.0-windows\win-x64\publish\
```

Publikacja jest:

- przeznaczona dla Windows x64,
- self-contained i zawiera wymagane środowisko .NET 8,
- przygotowana jako folder z `PdfToolbox.exe`, `PdfToolbox.Renderer.exe` i natywnymi bibliotekami PDFium,
- gotowa do przekazania użytkownikowi jako cały katalog `publish`.

Nie trzeba instalować .NET, Ghostscript, Pythona ani innych narzędzi.

### Uruchamianie w Visual Studio

1. Otwórz `PdfToolbox.sln`.
2. Ustaw `PdfToolbox.WinForms` jako projekt startowy.
3. Naciśnij F5 albo wybierz „Rozpocznij debugowanie”.

### Prywatność

> PDF Toolbox działa całkowicie lokalnie. Dokumenty nie są wysyłane do Internetu ani żadnych usług zewnętrznych.

- brak kodu wykonującego requesty HTTP,
- brak telemetrii, analytics i internetowych raportów awarii,
- brak analizowania lub indeksowania tekstu dokumentów,
- brak zapisywania treści PDF w logach,
- pliki częściowe są usuwane po błędzie lub anulowaniu,
- log lokalny nie zawiera nazw ani ścieżek dokumentów.

Wycinanie fragmentów stron również działa całkowicie lokalnie. Aplikacja nie pobiera PDFium podczas działania.

Logi techniczne są zapisywane w:

```text
%LocalAppData%\PdfToolbox\Logs\
```

### Podpisy elektroniczne

> Modyfikacja podpisanego dokumentu PDF może spowodować utratę ważności istniejącego podpisu. PDF Toolbox zawsze tworzy nowy dokument i nie modyfikuje oryginału.

Program sprawdza obecność pól podpisu typu `Sig` i wyświetla ostrzeżenie przed operacją. W przypadku wycinania fragmentów ostrzeżenie informuje dodatkowo, że wynikowe dokumenty są nowymi plikami i nie przenoszą oryginalnego podpisu.

### Dokumenty zabezpieczone

PDF Toolbox nie łamie haseł, nie usuwa zabezpieczeń i nie omija ograniczeń dokumentów. Jeżeli PDF nie może zostać bezpiecznie otwarty przez bibliotekę, operacja zostanie przerwana, a użytkownik zobaczy czytelny komunikat.

### Biblioteki i licencje

| Składnik | Wersja | Licencja | Zastosowanie |
|---|---:|---|---|
| PDF Toolbox | 1.0.0 | MIT | Kod aplikacji |
| .NET / Windows Forms | 8 | MIT | Środowisko uruchomieniowe i interfejs |
| PDFsharp | 6.2.4 | MIT | Odczyt, importowanie i zapis stron PDF |
| PDFiumCore | 154.0.8035 | Apache-2.0 | Izolowany binding .NET do lokalnego renderowania stron |
| PDFium | 154.0.8035 | Apache-2.0 | Natywny silnik renderowania PDF używany tylko do podglądu i rasteryzacji fragmentu |
| System.Drawing.Common | 8.0.11 | MIT | Przetwarzanie bitmap w helper-processie renderera |
| xUnit | 2.9.3 | Apache-2.0 | Testy — nie jest częścią opublikowanej aplikacji |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 | Uruchamianie testów |
| Microsoft.NET.Test.Sdk | 17.13.0 | MIT | Infrastruktura testowa |

Pełniejszy wykaz znajduje się w [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Projekt jest udostępniany na licencji MIT — zobacz [LICENSE](LICENSE).

### Security of PDF rendering dependency

PDFium jest używany wyłącznie do lokalnego renderowania podglądu strony i do bezpiecznej rasteryzacji zaznaczonego fragmentu przed utworzeniem nowego PDF.

- `PDFiumCore` jest przypięty do wersji `154.0.8035`,
- natywne binaria `PDFium` są przypięte do wersji `154.0.8035`,
- renderowanie działa w lokalnym helper-processie `PdfToolbox.Renderer.exe`,
- parsery PDF wymagają regularnych aktualizacji bezpieczeństwa,
- aktualizacji bezpieczeństwa PDFium nie należy ignorować.

### Ograniczenia

- chronione lub zaszyfrowane PDF-y mogą nie zostać otwarte,
- wykrywanie pól podpisu nie zastępuje weryfikacji kryptograficznej,
- operacje modyfikujące strukturę dokumentu mogą unieważnić istniejący podpis,
- wynik wycinania fragmentów jest tworzony przez rasteryzację zaznaczonego obszaru zamiast zachowania wektorowej zawartości strony,
- aplikacja nie obsługuje OCR, kompresji, edycji tekstu ani podpisywania PDF,
- bardzo duże lub nietypowo skonstruowane PDF-y mogą być ograniczone możliwościami PDFsharp, PDFium i dostępną pamięcią.

### Struktura projektu

```text
PdfToolbox.sln
src/PdfToolbox.WinForms/
  Assets/                   ikona aplikacji
  Controls/                 karty operacji, drag & drop, progress view i UI wycinania fragmentów
  Forms/                    główne okno
  Helpers/                  parser zakresów stron i mapowanie współrzędnych
  Models/                   modele dokumentów, operacji i zaznaczeń regionów
  Services/                 operacje PDF, walidacja, podgląd stron i wycinanie regionów
  Infrastructure/Logging/   lokalne logowanie techniczne
src/PdfToolbox.Renderer/    lokalny helper-process renderujący przez PDFium
tests/PdfToolbox.Tests/     testy jednostkowe i integracyjne
```

---

## English

### What is PDF Toolbox?

PDF Toolbox is a free Windows desktop application for basic PDF document operations. It is written in C# using .NET 8 and Windows Forms.

The application works entirely offline. Documents are not uploaded to the cloud or any external service. The program contains no telemetry, analytics, or online crash reporting.

### Features

- split one PDF into a separate file for every page,
- combine multiple PDFs into one document and reorder them,
- save only selected pages,
- create a copy without selected pages,
- rotate all pages or selected pages,
- sequentially split multiple documents,
- interactively extract PDF page regions into separate PDF files,
- drag-and-drop file selection,
- progress reporting and safe cancellation,
- automatic filename conflict handling without overwriting files,
- warnings when electronic-signature fields are detected.

Original documents are never modified or deleted.

### Interactive extraction of PDF page regions

The `Extract regions` operation lets the user:

- choose one PDF by drag and drop or file picker,
- navigate between pages,
- inspect a locally rendered preview of the real page,
- draw multiple rectangular selections,
- move, resize, and delete selections,
- choose an output folder,
- save each selected region as a separate PDF.

Output files use this naming pattern:

```text
{file}_page_{NNN}_region_{NN}.pdf
```

### Requirements

To build the project:

- Windows 10 or Windows 11 x64,
- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0),
- optionally Visual Studio 2022 with the “.NET desktop development” workload.

Users of the published application do not need Visual Studio, the .NET SDK, a separately installed .NET runtime, or any additional programs.

### How to build the project

Open PowerShell and navigate to the project directory:

```powershell
cd C:\Users\Kacper\Desktop\PDFSplitter
```

Then run:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

### How to publish the application

Run this command from the project root:

```powershell
dotnet publish src\PdfToolbox.WinForms\PdfToolbox.WinForms.csproj -c Release -r win-x64 --self-contained true
```

The finished application will be created in:

```text
src\PdfToolbox.WinForms\bin\Release\net8.0-windows\win-x64\publish\
```

The published application is:

- built for Windows x64,
- self-contained and includes the required .NET 8 runtime,
- packaged as a folder containing `PdfToolbox.exe`, `PdfToolbox.Renderer.exe`, and native PDFium binaries,
- ready to hand over as the full `publish` directory.

No separate installation of .NET, Ghostscript, Python, or other tools is required.

### Running in Visual Studio

1. Open `PdfToolbox.sln`.
2. Set `PdfToolbox.WinForms` as the startup project.
3. Press F5 or select “Start Debugging”.

### Privacy

> PDF Toolbox works entirely offline. Documents are not sent to the Internet or any external service.

- no HTTP request code,
- no telemetry, analytics, or online crash reports,
- no analysis or indexing of document text,
- no PDF content stored in logs,
- partial files are removed after errors or cancellation,
- local logs do not contain document names or paths.

Interactive region extraction also works entirely locally. The application does not download PDFium at runtime.

Technical logs are stored in:

```text
%LocalAppData%\PdfToolbox\Logs\
```

### Electronic signatures

> Modifying a signed PDF may invalidate its existing electronic signature. PDF Toolbox always creates a new document and never modifies the original.

The program checks for `Sig` signature fields and displays a warning before an operation. During region extraction, the warning also explains that the output is created as a new PDF and does not preserve the original signature.

### Protected documents

PDF Toolbox does not crack passwords, remove protection, or bypass document restrictions. If the PDF library cannot safely open a document, the operation is stopped and a clear message is displayed.

### Libraries and licenses

| Component | Version | License | Purpose |
|---|---:|---|---|
| PDF Toolbox | 1.0.0 | MIT | Application source code |
| .NET / Windows Forms | 8 | MIT | Runtime and user interface |
| PDFsharp | 6.2.4 | MIT | Reading, importing, and writing PDF pages |
| PDFiumCore | 154.0.8035 | Apache-2.0 | Isolated .NET bindings for local page rendering |
| PDFium | 154.0.8035 | Apache-2.0 | Native PDF renderer used only for preview and region rasterization |
| System.Drawing.Common | 8.0.11 | MIT | Bitmap processing inside the rendering helper process |
| xUnit | 2.9.3 | Apache-2.0 | Tests; not included in the published application |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 | Test runner |
| Microsoft.NET.Test.Sdk | 17.13.0 | MIT | Test infrastructure |

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for a more complete list. This project is released under the MIT License — see [LICENSE](LICENSE).

### Security of PDF rendering dependency

PDFium is used only for local page rendering and secure rasterization of the selected region before a new PDF is created.

- `PDFiumCore` is pinned to `154.0.8035`,
- the native `PDFium` binaries are pinned to `154.0.8035`,
- rendering is isolated in the local `PdfToolbox.Renderer.exe` helper process,
- PDF parsers receive security fixes over time and should be reviewed regularly,
- PDFium security updates should not be ignored.

### Limitations

- protected or encrypted PDFs may not be opened,
- signature-field detection is not a replacement for cryptographic verification,
- operations that modify the document structure may invalidate existing signatures,
- region extraction outputs are created by rasterizing the selected area instead of preserving vector page contents,
- the application does not provide OCR, compression, text editing, or PDF signing,
- extremely large or unusually constructed PDFs may be limited by PDFsharp, PDFium, and available memory.

### Project structure

```text
PdfToolbox.sln
src/PdfToolbox.WinForms/
  Assets/                   application icon
  Controls/                 operation cards, drag and drop, progress view, and region extraction UI
  Forms/                    main window
  Helpers/                  page-range parser and coordinate mapping
  Models/                   document, operation, and page-region models
  Services/                 PDF operations, validation, previews, and region extraction
  Infrastructure/Logging/   local technical logging
src/PdfToolbox.Renderer/    local PDFium-based helper process
tests/PdfToolbox.Tests/     unit and integration tests
```
