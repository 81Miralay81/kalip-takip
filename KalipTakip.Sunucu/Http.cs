using System;
using System.Collections.Specialized;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace KalipTakip.Sunucu
{
    /// <summary>Çok küçük HTTP/1.1 sunucu katmanı (TcpListener): yönetici izni gerektirmez.</summary>
    internal sealed class Istek
    {
        public string HttpMethod;
        public Uri Url;
        public NameValueCollection QueryString = new NameValueCollection();
        public NameValueCollection Headers = new NameValueCollection();
        public Stream InputStream = new MemoryStream();
    }

    internal sealed class Yanit
    {
        public int StatusCode = 200;
        public string ContentType = "text/plain";
        public NameValueCollection Headers = new NameValueCollection();
        public long ContentLength64;
        public MemoryStream OutputStream = new MemoryStream();
        internal Stream Hedef;
        private bool _kapali;

        public void Close()
        {
            if (_kapali) return;
            _kapali = true;
            byte[] g = OutputStream.ToArray();
            StringBuilder sb = new StringBuilder();
            sb.Append("HTTP/1.1 ").Append(StatusCode).Append(' ').Append(StatusCode == 200 ? "OK" : "Hata").Append("\r\n");
            sb.Append("Content-Type: ").Append(ContentType).Append("\r\n");
            sb.Append("Content-Length: ").Append(g.Length).Append("\r\n");
            foreach (string k in Headers.AllKeys) sb.Append(k).Append(": ").Append(Headers[k]).Append("\r\n");
            sb.Append("Connection: close\r\n\r\n");
            byte[] b = Encoding.UTF8.GetBytes(sb.ToString());
            Hedef.Write(b, 0, b.Length);
            Hedef.Write(g, 0, g.Length);
            Hedef.Flush();
        }
    }

    internal sealed class Baglanti
    {
        public Istek Request = new Istek();
        /// <summary>İstemcinin IP adresi (bağlantıdan).</summary>
        public string IstemciIp = "";
        public Yanit Response = new Yanit();
        private readonly TcpClient _istemci;

        private Baglanti(TcpClient c) { _istemci = c; }

        public void Kapat() { try { _istemci.Close(); } catch { } }

        /// <summary>İstemciden isteği okur; geçersizse null.</summary>
        public static Baglanti Oku(TcpClient c, Stream akis = null)
        {
            c.ReceiveTimeout = 15000;
            c.SendTimeout = 15000;
            Stream ag = akis ?? c.GetStream();
            MemoryStream ham = new MemoryStream();
            byte[] tampon = new byte[4096];
            int basSonu = -1;
            while (basSonu < 0)
            {
                int n = ag.Read(tampon, 0, tampon.Length);
                if (n <= 0) return null;
                ham.Write(tampon, 0, n);
                byte[] a = ham.GetBuffer();
                for (int i = 3; i < ham.Length; i++)
                    if (a[i] == '\n' && a[i - 1] == '\r' && a[i - 2] == '\n' && a[i - 3] == '\r') { basSonu = i + 1; break; }
                if (ham.Length > 65536 && basSonu < 0) return null;
            }
            byte[] tum = ham.ToArray();
            string[] satirlar = Encoding.UTF8.GetString(tum, 0, basSonu).Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] ilk = satirlar[0].Split(' ');
            if (ilk.Length < 2) return null;
            Baglanti b = new Baglanti(c);
            try { System.Net.IPEndPoint uc = c.Client.RemoteEndPoint as System.Net.IPEndPoint; if (uc != null) b.IstemciIp = uc.Address.IsIPv4MappedToIPv6 ? uc.Address.MapToIPv4().ToString() : uc.Address.ToString(); } catch { }
            b.Request.HttpMethod = ilk[0].ToUpperInvariant();
            string hedef = ilk[1];
            string yol = hedef, sorgu = "";
            int q = hedef.IndexOf('?');
            if (q >= 0) { yol = hedef.Substring(0, q); sorgu = hedef.Substring(q + 1); }
            b.Request.Url = new Uri("http://yerel" + (yol.StartsWith("/") ? yol : "/" + yol));
            foreach (string p in sorgu.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int e = p.IndexOf('=');
                string ad = Coz(e < 0 ? p : p.Substring(0, e));
                string de = e < 0 ? "" : Coz(p.Substring(e + 1));
                b.Request.QueryString[ad] = de;
            }
            int uzunluk = 0;
            for (int i = 1; i < satirlar.Length; i++)
            {
                int k = satirlar[i].IndexOf(':');
                if (k <= 0) continue;
                string ad = satirlar[i].Substring(0, k).Trim();
                string de = satirlar[i].Substring(k + 1).Trim();
                b.Request.Headers[ad] = de;
                if (string.Equals(ad, "Content-Length", StringComparison.OrdinalIgnoreCase)) int.TryParse(de, out uzunluk);
            }
            if (uzunluk > 40 * 1048576) return null;
            MemoryStream govde = new MemoryStream();
            int var = tum.Length - basSonu;
            if (var > 0) govde.Write(tum, basSonu, var);
            while (govde.Length < uzunluk)
            {
                int n = ag.Read(tampon, 0, tampon.Length);
                if (n <= 0) break;
                govde.Write(tampon, 0, n);
            }
            govde.Position = 0;
            b.Request.InputStream = govde;
            b.Response.Hedef = ag;
            return b;
        }

        private static string Coz(string s)
        {
            return Uri.UnescapeDataString(s.Replace('+', ' '));
        }
    }
}
