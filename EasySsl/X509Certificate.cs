﻿using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Asn1;
using EasySsl.Extensions;
using EasySsl.Utils;
using System.Collections.Generic;

namespace EasySsl {
    public class X509Certificate {

        public X509Certificate() {
            Tbs = new X509TbsCertificate();
        }

        //https://tools.ietf.org/html/rfc5280#section-4.1
        public X509Certificate(Asn1Sequence node) {
            var i = 0;
            var subnode = node.Nodes[i++];

            Tbs = new X509TbsCertificate((Asn1Sequence)subnode);
            subnode = node.Nodes[i++];

            SignatureAlgorithm = new X509AlgorithmIdentifier((Asn1Sequence)subnode);
            subnode = node.Nodes[i++];

            SignatureValue = (Asn1BitString)subnode;
        }


        public X509TbsCertificate Tbs { get; set; }

        public X509AlgorithmIdentifier SignatureAlgorithm { get; set; }

        public Asn1BitString SignatureValue { get; set; }

        public X509PrivateKey PrivateKey { get; set; }

        public Asn1Node ToAsn1() {
            if (SignatureValue == null) throw new Exception("Certificate is not signed");
            return new Asn1Sequence {
                Nodes = {
                    Tbs.ToAsn1(),
                    SignatureAlgorithm.ToAsn1(),
                    SignatureValue
                }
            };
        }

        public X509Certificate AddSubjectAltNames(params string[] names) {
            var ext = Tbs.Extensions.GetExtension<SubjectAltNameExtension>();
            if (ext == null) {
                ext = new SubjectAltNameExtension();
                Tbs.Extensions.Add(ext);
            }

            foreach (var name in names) {
                ext.Names.Add(name);
            }

            return this;
        }

        public X509Certificate SetAuthorityInfoAccess(AuthorityInfoAccessExtension data) {
            Tbs.Extensions.SetAuthorityInfoAccess(data);
            return this;
        }

        public X509Certificate GenerateRsaKey(int keySize = 2048) {
            var privateKey = new RsaPrivateKey(keySize);
            var publicKey = privateKey.CreatePublicKey();
            Tbs.SubjectPublicKeyInfo = publicKey.GetSubjectPublicKeyInfo();
            PrivateKey = privateKey;
            return SetSubjectKeyIdentifier();
        }

        public X509Certificate SetPrivateKey(X509PrivateKey privateKey) {
            this.PrivateKey = privateKey;
            return this;
        }

        public X509Certificate GenerateSerialNumber() {
            using (var rnd = RandomNumberGenerator.Create()) {
                var res = new byte[20];
                rnd.GetBytes(res);
                res[0] &= 0x7f;
                Tbs.SerialNumber = new Asn1Integer(res);
                return this;
            }
        }

        public X509Certificate SignWith(X509Certificate authority) {
            Tbs.Extensions.SetAuthorityKeyIdentifier(authority.Tbs.Extensions.GetSubjectKeyIdentifier());
            SignWith(authority.PrivateKey);
            return this;
        }

        public X509Certificate AddExtensions(params X509Extension[] extensions) {
            foreach (var ext in extensions) {
                Tbs.Extensions.Add(ext);
            }
            return this;
        }

        public X509Certificate AddExtensions(IEnumerable<X509Extension> extensions) {
            foreach (var ext in extensions) {
                Tbs.Extensions.Add(ext);
            }
            return this;
        }

        public X509Certificate SignSelf() {
            return SignWith(this);
        }

        public X509Certificate SignWith(X509PrivateKey authorityPrivateKey) {
            var tbsData = Tbs.ToAsn1().GetBytes();
            var data = authorityPrivateKey.SignData(tbsData);
            SignatureValue = new Asn1BitString(data);
            SignatureAlgorithm = Tbs.SignatureAlgorithm;
            return this;
        }

        public X509Certificate SetIssuer(X509Certificate authority) {
            Tbs.Issuer = authority.Tbs.Subject;
            return this;
        }

        public X509Certificate SetIssuerSelf() {
            return SetIssuer(this);
        }

        public X509Certificate SetBasicConstraint(BasicConstraintExtension data) {
            Tbs.Extensions.SetBasicConstraint(data);
            return this;
        }

        public X509Certificate SetSubjectKeyIdentifier(byte[] data) {
            Tbs.Extensions.SetSubjectKeyIdentifier(data);
            return this;
        }

        public X509Certificate SetSubjectKeyIdentifier() {
            return SetSubjectKeyIdentifier(Tbs.SubjectPublicKeyInfo.GenerateIdentifier());
        }

        public string ToPem() {
            var asn1 = ToAsn1();
            var data = asn1.GetBytes();
            var str = Base64.Convert(data);
            str = "-----BEGIN CERTIFICATE-----\r\n" + str + "\r\n-----END CERTIFICATE-----";
            return str;
        }

        public override string ToString() {
            var writer = new StringWriter();
            var pb = new PrettyBuilder(writer);
            pb.Append($@"Data:
  Version:  v3
  Serial Number: {Tbs.SerialNumber.GetBytes().GetHexString()}
  Signature Algorithm: SHA1withRSA - 1.2.840.113549.1.1.5
  Issuer: {Tbs.Issuer}
  Validity: 
    Not Before: {Tbs.Validity.NotBefore}
    Not  After: {Tbs.Validity.NotAfter}
  Subject: {Tbs.Subject}
  Subject Public Key Info: 
    Algorithm: RSA - {Tbs.SubjectPublicKeyInfo.Algorithm}
    Public Key: 
      Exponent: 65537
      Public Key Modulus: (2048 bits) :
        E4:71:2A:CE:E4:24:DC:C4:AB:DF:A3:2E:80:42:0B:D9:
        CF:90:BE:88:4A:5C:C5:B3:73:BF:49:4D:77:31:8A:88:
        15:A7:56:5F:E4:93:68:83:00:BB:4F:C0:47:03:67:F1:
        30:79:43:08:1C:28:A8:97:70:40:CA:64:FA:9E:42:DF:
        35:3D:0E:75:C6:B9:F2:47:0B:D5:CE:24:DD:0A:F7:84:
        4E:FA:16:29:3B:91:D3:EE:24:E9:AF:F6:A1:49:E1:96:
        70:DE:6F:B2:BE:3A:07:1A:0B:FD:FE:2F:75:FD:F9:FC:
        63:69:36:B6:5B:09:C6:84:92:17:9C:3E:64:C3:C4:C9
  Signature: 
    Algorithm: SHA1withRSA - 1.2.840.113549.1.1.5
    Signature: 
      AA:96:65:3D:10:FA:C7:0B:74:38:2D:93:54:32:C0:5B:
      2F:18:93:E9:7C:32:E6:A4:4F:4E:38:93:61:83:3A:6A:
      A2:11:91:C2:D2:A3:48:07:6C:07:54:A8:B8:42:0E:B4:
      E4:AE:42:B4:B5:36:24:46:4F:83:61:64:13:69:03:DF:
      41:88:0B:CB:39:57:8C:6B:9F:52:7E:26:F9:24:5E:E7:
      BC:FB:FD:93:13:AF:24:3A:8F:DB:E3:DC:C9:F9:1F:67:
      A8:BD:0B:95:84:9D:EB:FC:02:95:A0:49:2C:05:D4:B0:
      35:EA:A6:80:30:20:FF:B1:85:C8:4B:74:D9:DC:BB:50");
            if (Tbs.Extensions.Any()) {
                pb.Append("Extensions:");
                pb.IndentRight();
                foreach (var ext in Tbs.Extensions) {
                    pb.Append($"Identifier: {ext.Id.FriendlyName} - {ext.Id.Value}");
                    pb.IndentRight();
                    pb.Append($"Critical: {(ext.Critical ? "yes" : "no")}");
                    pb.Append(ext.ToString());
                    pb.IndentLeft();
                }
                pb.IndentLeft();

            }
            return writer.GetStringBuilder().ToString();
        }

        public X509Certificate Export(string filePath, bool includePrivate = false) {
            using (var file = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None)) {
                using (var writer = new StreamWriter(file)) {
                    var pem = ToPem();
                    writer.Write(pem);

                    if (PrivateKey != null && includePrivate) {
                        writer.WriteLine();
                        writer.WriteLine();
                        writer.Write(PrivateKey.ToPem());
                    }

                    writer.Flush();
                }
                return this;
            }
        }

        public X509Certificate ExportPrivateKey(string filePath) {
            if (PrivateKey == null) {
                throw new InvalidOperationException("This certificate doesn't have private key");
            }
            PrivateKey.Export(filePath);
            return this;
        }

        public void ExportPvk(string filePath) {
            if (PrivateKey == null) {
                throw new InvalidOperationException("Private Key is not set");
            }

            var data = PrivateKey.ToPvk();
            using (var file = File.OpenWrite(filePath)) {
                file.Write(data, 0, data.Length);
                file.Flush();
            }
        }

    }
}
