namespace Axial.Tests

open System
open System.Net
open System.Net.WebSockets
open System.Security.Cryptography
open System.Security.Cryptography.X509Certificates
open System.Threading
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Server.Kestrel.Core
open Microsoft.AspNetCore.Server.Kestrel.Https
open Microsoft.Extensions.Hosting

module TlsServer =
    let makeCa () =
        use key = RSA.Create(2048)
        let req = CertificateRequest("CN=Axial Test CA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
        req.CertificateExtensions.Add(X509BasicConstraintsExtension(true, false, 0, true))
        req.CertificateExtensions.Add(X509SubjectKeyIdentifierExtension(req.PublicKey, false))
        req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1.), DateTimeOffset.UtcNow.AddDays(1.))

    let issue (ca: X509Certificate2) (subject: string) (isServer: bool) =
        use key = RSA.Create(2048)
        let req = CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
        req.CertificateExtensions.Add(X509BasicConstraintsExtension(false, false, 0, true))
        req.CertificateExtensions.Add(X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true))
        let ekus = OidCollection()
        ekus.Add(Oid(if isServer then "1.3.6.1.5.5.7.3.1" else "1.3.6.1.5.5.7.3.2")) |> ignore
        req.CertificateExtensions.Add(X509EnhancedKeyUsageExtension(ekus, false))
        if isServer then
            let san = SubjectAlternativeNameBuilder()
            san.AddDnsName(if subject = "CN=other.example" then "other.example" else "localhost")
            if subject <> "CN=other.example" then san.AddIpAddress(IPAddress.Loopback)
            req.CertificateExtensions.Add(san.Build())
        let serial = RandomNumberGenerator.GetBytes(16)
        use signed = req.Create(ca, DateTimeOffset.UtcNow.AddHours(-1.), DateTimeOffset.UtcNow.AddHours(12.), serial)
        let cert = signed.CopyWithPrivateKey(key)
        if isServer then
            let pfx = cert.Export(X509ContentType.Pfx)
            cert.Dispose()
            X509CertificateLoader.LoadPkcs12(pfx, null)
        else cert

    let start (serverCert: X509Certificate2) (clientCa: X509Certificate2 option) = task {
        let builder = WebApplication.CreateSlimBuilder()
        builder.WebHost.ConfigureKestrel(fun kestrel ->
            // Kestrel rejects port zero for ListenLocalhost; loopback IP supports dynamic binding.
            kestrel.Listen(IPAddress.Loopback, 0, fun listen ->
                listen.UseHttps(fun https ->
                    https.ServerCertificate <- serverCert
                    https.ClientCertificateMode <- if clientCa.IsSome then ClientCertificateMode.RequireCertificate else ClientCertificateMode.NoCertificate
                    https.ClientCertificateValidation <- fun cert _ _ ->
                        match clientCa with
                        | None -> true
                        | Some root ->
                            use chain = new X509Chain()
                            chain.ChainPolicy.TrustMode <- X509ChainTrustMode.CustomRootTrust
                            chain.ChainPolicy.CustomTrustStore.Add(root) |> ignore
                            chain.ChainPolicy.RevocationMode <- X509RevocationMode.NoCheck
                            chain.Build(cert)) |> ignore) |> ignore) |> ignore
        let app = builder.Build()
        app.UseWebSockets() |> ignore
        app.Map("/ws", RequestDelegate(fun (context: HttpContext) -> task {
            if context.WebSockets.IsWebSocketRequest then
                use! socket = context.WebSockets.AcceptWebSocketAsync()
                let buffer = Array.zeroCreate<byte> 8192
                let mutable active = true
                while active do
                    let! result = socket.ReceiveAsync(Memory<byte>(buffer), CancellationToken.None)
                    if result.MessageType = WebSocketMessageType.Close then
                        active <- false
                    else
                        let! sent = socket.SendAsync(ReadOnlyMemory<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None)
                        sent |> ignore
            else context.Response.StatusCode <- 400
        })) |> ignore
        do! app.StartAsync()
        let port = app.Urls |> Seq.head |> Uri |> fun uri -> uri.Port
        return port, (app :> IAsyncDisposable)
    }
