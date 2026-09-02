# Third-party notices

PDF Toolbox itself is licensed under the MIT License. Test-only dependencies are not included in the published application.

## Runtime dependencies

| Package/component | Version | License |
|---|---:|---|
| PDFsharp | 6.2.4 | MIT |
| PDFiumCore | 154.0.8035 | Apache-2.0 |
| bblanchon.PDFium.Win32 | 154.0.8035 | Apache-2.0 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | MIT |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | MIT |
| System.Drawing.Common | 8.0.11 | MIT |
| System.Security.Cryptography.Pkcs | 8.0.1 | MIT |
| .NET runtime and Windows Forms | 8 | MIT |

PDFsharp project and license: https://github.com/empira/PDFsharp
PDFiumCore project and license: https://github.com/Dtronix/PDFiumCore
PDFium Windows binaries project and license: https://github.com/bblanchon/pdfium-binaries

## Additional notices for PDF rendering

- PDFiumCore 154.0.8035 is licensed under Apache-2.0.
- bblanchon.PDFium.Win32 154.0.8035 is licensed under Apache-2.0.
- The bblanchon.PDFium.Win32 NuGet package carries the notice `Copyright © Benoît Blanchon 2017-2025`.
- PDFium itself is an Apache-2.0 licensed project used here only for local rendering in `PdfToolbox.Renderer.exe`.

## Test-only dependencies

| Package/component | Version | License |
|---|---:|---|
| xUnit and xUnit extensibility packages | 2.9.3 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 |
| xunit.abstractions | 2.0.3 | Apache-2.0 |
| xunit.analyzers | 1.18.0 | Apache-2.0 |
| Microsoft.NET.Test.Sdk | 17.13.0 | MIT |
| Microsoft.TestPlatform packages | 17.13.0 | MIT |
| Microsoft.CodeCoverage | 17.13.0 | MIT |
| Newtonsoft.Json | 13.0.1 | MIT |
| System.Reflection.Metadata | 1.6.0 | MIT |

License expressions and license links are included in the NuGet metadata of the respective packages.

PDF Toolbox does not include or require Ghostscript, Python, Chromium, LibreOffice, a cloud service, or another PDF library.
