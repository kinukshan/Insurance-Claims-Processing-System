using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.RiskAssessment.Enums;

namespace InsuranceClaims.Application.ClaimsManagement.Services;

/// <summary>
/// Deterministic validator for document integrity, file consistency, and content credibility.
/// Does not prove legal authenticity; detects obvious inconsistencies, corrupt files,
/// duplicate file reuse, and cross-type document mismatches.
/// Authoritative and deterministic.
/// </summary>
public static class DocumentIntegrityValidator
{
    private const int MaxParseSizeBytes = 25 * 1024 * 1024; // 25 MB

    /// <summary>
    /// Maps DocumentType to allowed file signatures (magic byte formats).
    /// Documents whose actual file signature is not in this set are rejected.
    /// Types not listed here accept any non-empty file (backward compatible).
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> AllowedFileSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Photos of Damage"] = new(StringComparer.OrdinalIgnoreCase) { "jpeg", "png", "webp" },
        ["Police Report"] = new(StringComparer.OrdinalIgnoreCase) { "pdf" },
        ["Repair Estimate"] = new(StringComparer.OrdinalIgnoreCase) { "pdf" },
        ["Driver License"] = new(StringComparer.OrdinalIgnoreCase) { "jpeg", "png", "webp", "pdf" },
        ["Death Certificate"] = new(StringComparer.OrdinalIgnoreCase) { "pdf" },
        ["Medical Report"] = new(StringComparer.OrdinalIgnoreCase) { "pdf" },
        ["Hospital Bills"] = new(StringComparer.OrdinalIgnoreCase) { "pdf" },
        ["Prescription"] = new(StringComparer.OrdinalIgnoreCase) { "pdf", "jpeg", "png" },
        ["Doctor Referral"] = new(StringComparer.OrdinalIgnoreCase) { "pdf" },
        ["Property Deed"] = new(StringComparer.OrdinalIgnoreCase) { "pdf" },
        ["Property Valuation"] = new(StringComparer.OrdinalIgnoreCase) { "pdf" },
        ["Beneficiary / Nominee Identification"] = new(StringComparer.OrdinalIgnoreCase) { "pdf", "jpeg", "png" },
        ["Policy Document"] = new(StringComparer.OrdinalIgnoreCase) { "pdf" },
        ["Claim Form"] = new(StringComparer.OrdinalIgnoreCase) { "pdf" },
        ["Travel Itinerary"] = new(StringComparer.OrdinalIgnoreCase) { "pdf" },
    };

    private static readonly Dictionary<string, (string[] Primary, string[] Secondary)> DocumentSignals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Police Report"] = (
            new[] { "police", "station", "officer", "collision", "fir", "general diary", "gd entry", "law enforcement", "constable", "patrol", "traffic police", "traffic accident", "motor vehicle accident" },
            new[] { "accident", "incident", "report", "investigation", "witness", "damage", "vehicle" }
        ),
        ["Repair Estimate"] = (
            new[] { "repair", "estimate", "labour", "labor", "parts", "workshop", "garage", "quotation", "contractor", "body shop", "mechanic", "body repair", "automotive" },
            new[] { "total", "replacement", "cost", "materials", "subtotal", "tax", "hours", "estimated" }
        ),
        ["Driver License"] = (
            new[] { "driver", "driving", "licence", "license", "permit", "class of vehicle", "driving licence", "driver license" },
            new[] { "date of birth", "dob", "expiry", "validity", "issued", "license number", "licence number" }
        ),
        ["Death Certificate"] = (
            new[] { "death", "deceased", "cause of death", "coroner", "died", "burial", "death certificate", "certify the death" },
            new[] { "certificate", "registrar", "hospital", "medical", "date of death" }
        ),
        ["Beneficiary / Nominee Identification"] = (
            new[] { "beneficiary", "nominee", "relationship to insured", "nominee identification", "beneficiary identification", "kin", "spouse" },
            new[] { "identification", "identity", "national id", "passport", "full name", "nic" }
        ),
        ["Policy Document"] = (
            new[] { "policy schedule", "policy document", "policyholder", "sum assured", "premium payable", "terms and conditions" },
            new[] { "policy", "coverage", "insured", "insurance", "underwriter" }
        ),
        ["Claim Form"] = (
            new[] { "claim form", "claimant declaration", "signature of claimant", "claim details" },
            new[] { "claimant", "declaration", "policy number", "loss" }
        ),
        ["Medical Report"] = (
            new[] { "medical report", "clinical diagnosis", "physician", "treatment plan", "hospitalization", "attending physician" },
            new[] { "medical", "patient", "doctor", "diagnosis", "examination", "treatment" }
        ),
        ["Hospital Bills"] = (
            new[] { "hospital bill", "invoice", "room charges", "admission fee", "discharge summary", "pharmacy charges" },
            new[] { "bill", "total", "charges", "patient", "hospital" }
        ),
        ["Prescription"] = (
            new[] { "prescription", "rx", "dosage", "dispensed", "medication", "pharmacy" },
            new[] { "doctor", "instructions", "tablets", "capsules", "prescribed" }
        ),
        ["Doctor Referral"] = (
            new[] { "referral letter", "referring physician", "consultant", "referred to" },
            new[] { "referral", "doctor", "specialist", "patient" }
        ),
        ["Property Deed"] = (
            new[] { "title deed", "property deed", "conveyance", "land registry", "cadastral", "parcel number" },
            new[] { "deed", "property", "owner", "ownership", "land" }
        ),
        ["Property Valuation"] = (
            new[] { "valuation report", "property valuation", "appraisal", "surveyor", "market valuation" },
            new[] { "valuation", "property", "assessed value", "replacement cost" }
        ),
        ["Travel Itinerary"] = (
            new[] { "flight", "boarding pass", "e-ticket", "airline", "passenger", "booking reference", "itinerary" },
            new[] { "travel", "departure", "arrival", "hotel", "reservation" }
        )
    };

    public static string NormalizeDocumentType(string documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType)) return string.Empty;
        var trimmed = documentType.Trim();
        if (string.Equals(trimmed, "Beneficiary ID", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Beneficiary / Nominee Identification", StringComparison.OrdinalIgnoreCase))
        {
            return "Beneficiary / Nominee Identification";
        }
        return trimmed;
    }

    public static string ComputeSha256(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return string.Empty;
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static string DetectFileSignature(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 4) return "empty";
        if (bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46) return "pdf"; // %PDF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "jpeg";
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A) return "png";
        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && // RIFF
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return "webp"; // WEBP
        // DOCX / XLSX / ZIP archives (PK\x03\x04)
        if (bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04) return "zip_archive";
        return "unknown";
    }

    /// <summary>
    /// Checks whether the detected file signature is allowed for the given document type.
    /// Returns (isCompatible, reason).
    /// </summary>
    public static (bool IsCompatible, string Reason) CheckFormatCompatibility(string detectedSignature, string normalizedDocType)
    {
        if (!AllowedFileSignatures.TryGetValue(normalizedDocType, out var allowed))
        {
            // Document type not in the mapping — allow any non-empty file (backward compatible)
            return (true, $"Document type '{normalizedDocType}' accepts any valid file format.");
        }

        if (allowed.Contains(detectedSignature))
        {
            return (true, $"File format '{detectedSignature}' is valid for '{normalizedDocType}'.");
        }

        var acceptedList = string.Join(", ", allowed);
        return (false, $"File type '{detectedSignature}' is not valid for '{normalizedDocType}'. Accepted formats: {acceptedList}.");
    }

    /// <summary>
    /// Fallback extension mapping for when file bytes are not yet loaded or in tests.
    /// Actual file signature check remains authoritative whenever file bytes are available.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Photos of Damage"] = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" },
        ["Police Report"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" },
        ["Repair Estimate"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" },
        ["Driver License"] = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".pdf" },
        ["Death Certificate"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" },
        ["Medical Report"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" },
        ["Hospital Bills"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" },
        ["Prescription"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png" },
        ["Doctor Referral"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" },
        ["Property Deed"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" },
        ["Property Valuation"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" },
        ["Beneficiary / Nominee Identification"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png" },
        ["Policy Document"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" },
        ["Claim Form"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" },
        ["Travel Itinerary"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" },
    };

    public static bool IsFormatCompatibleWithExtension(string fileName, string normalizedDocType)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.TryGetValue(normalizedDocType, out var allowed))
        {
            return true;
        }
        return allowed.Contains(ext);
    }

    public static string ExtractTextSafely(byte[] bytes, string fileName)
    {
        if (bytes == null || bytes.Length == 0) return string.Empty;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".txt" || ext == ".csv" || ext == ".log")
        {
            return Encoding.UTF8.GetString(bytes);
        }

        if (bytes.Length >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46)
        {
            return ExtractPdfText(bytes);
        }

        // For non-PDF binary files (e.g. images), do not extract text
        return string.Empty;
    }

    private static string ExtractPdfText(byte[] bytes)
    {
        var sb = new StringBuilder();
        var rawString = Encoding.Latin1.GetString(bytes);

        // 1. Literal text objects in uncompressed streams or text blocks: (...) Tj or [...] TJ
        var tjMatches = Regex.Matches(rawString, @"\(([^)]+)\)\s*(?:Tj|')");
        foreach (Match m in tjMatches)
        {
            sb.Append(m.Groups[1].Value).Append(' ');
        }

        // 2. Scan for FlateDecode streams and decompress them
        var streamMatches = Regex.Matches(rawString, @"stream[\r\n]+([\s\S]*?)[\r\n]+endstream");
        foreach (Match m in streamMatches)
        {
            var streamContent = m.Groups[1].Value;
            var headerIdx = Math.Max(0, m.Index - 300);
            var header = rawString.Substring(headerIdx, m.Index - headerIdx);

            if (header.Contains("FlateDecode", StringComparison.OrdinalIgnoreCase))
            {
                byte[]? decompressed = null;

                if (header.Contains("ASCII85Decode", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var a85Decoded = DecodeAscii85(streamContent.Trim());
                        decompressed = DecompressZlib(a85Decoded);
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        var streamBytes = Encoding.Latin1.GetBytes(streamContent);
                        decompressed = DecompressZlib(streamBytes);
                    }
                    catch { }
                }

                if (decompressed != null)
                {
                    var decText = Encoding.Latin1.GetString(decompressed);
                    var innerTj = Regex.Matches(decText, @"\(([^)]+)\)\s*(?:Tj|')");
                    foreach (Match it in innerTj)
                    {
                        sb.Append(it.Groups[1].Value).Append(' ');
                    }
                }
            }
        }

        // 3. Fallback: extract any visible readable words in parenthesis
        if (sb.Length < 30)
        {
            var direct = Regex.Matches(rawString, @"\(([A-Za-z0-9 ,._/:;\-]{4,})\)");
            foreach (Match d in direct)
            {
                sb.Append(d.Groups[1].Value).Append(' ');
            }
        }

        return sb.ToString().Trim();
    }

    private static byte[] DecompressZlib(byte[] data)
    {
        if (data.Length < 2) return Array.Empty<byte>();

        // ZLib has 2 header bytes (usually 0x78 0x9C, 0x78 0x01, 0x78 0xDA)
        try
        {
            using var ms = new MemoryStream(data);
            using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            zlib.CopyTo(outMs);
            return outMs.ToArray();
        }
        catch
        {
            // Try DeflateStream without zlib header
            try
            {
                using var ms2 = new MemoryStream(data, 2, data.Length - 2);
                using var deflate = new DeflateStream(ms2, CompressionMode.Decompress);
                using var outMs2 = new MemoryStream();
                deflate.CopyTo(outMs2);
                return outMs2.ToArray();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }
    }

    private static byte[] DecodeAscii85(string ascii85)
    {
        // Strip ~> trailer and whitespace
        var clean = Regex.Replace(ascii85, @"\s+", "");
        if (clean.EndsWith("~>")) clean = clean.Substring(0, clean.Length - 2);
        if (clean.StartsWith("<~")) clean = clean.Substring(2);

        using var ms = new MemoryStream();
        uint tuple = 0;
        int count = 0;

        foreach (char c in clean)
        {
            if (c == 'z' && count == 0)
            {
                ms.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
                continue;
            }
            if (c < '!' || c > 'u') continue;

            tuple |= (uint)(c - '!') * Pow85(4 - count);
            count++;

            if (count == 5)
            {
                ms.WriteByte((byte)(tuple >> 24));
                ms.WriteByte((byte)(tuple >> 16));
                ms.WriteByte((byte)(tuple >> 8));
                ms.WriteByte((byte)tuple);
                tuple = 0;
                count = 0;
            }
        }

        if (count > 0)
        {
            for (int i = count; i < 5; i++)
            {
                tuple |= (uint)('u' - '!') * Pow85(4 - i);
            }
            for (int i = 0; i < count - 1; i++)
            {
                ms.WriteByte((byte)(tuple >> (24 - 8 * i)));
            }
        }

        return ms.ToArray();
    }

    private static uint Pow85(int exp)
    {
        uint res = 1;
        for (int i = 0; i < exp; i++) res *= 85;
        return res;
    }

    private static bool ContainsAny(string textLower, params string[] keywords)
    {
        return keywords.Any(k => textLower.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountMatches(string textLower, params string[] keywords)
    {
        return keywords.Count(k => textLower.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Deterministically validates whether extracted text contains credible evidence
    /// matching the declared DocumentType category.
    /// Uses grouped signals / threshold logic so legitimate variation is accepted
    /// without requiring an exact hard-coded template.
    /// </summary>
    public static bool CheckCategoryValidity(string normalizedDocType, string textLower)
    {
        if (string.IsNullOrWhiteSpace(textLower) || textLower.Length < 30)
            return false;

        switch (normalizedDocType)
        {
            case "Doctor Referral":
            {
                // Group 1: Referral intent/context
                var hasReferralIntent = ContainsAny(textLower,
                    "referral", "referred", "refer", "referring", "kindly evaluate",
                    "please evaluate", "specialist", "referred to", "consultation request",
                    "evaluate the patient", "for evaluation", "for review", "further review",
                    "further management", "second opinion", "referral letter", "referral reason");

                // Group 2: Medical / clinical context
                var hasMedicalContext = ContainsAny(textLower,
                    "doctor", "physician", "dr.", "dr ", "consultant", "patient",
                    "clinic", "hospital", "medical", "clinical");

                // Group 3: Referral specifics (referring doctor, patient, recipient department, referral reason)
                var hasReferralDetails = ContainsAny(textLower,
                    "referring doctor", "referring physician", "referring dr", "general physician",
                    "patient", "patient id", "patient name", "department", "orthopedic",
                    "cardiology", "neurology", "oncology", "surgery", "referral reason",
                    "reason for referral", "evaluation", "assessment", "complaint", "condition",
                    "symptoms", "injury", "specialist", "consultant");

                return hasReferralIntent && hasMedicalContext && hasReferralDetails;
            }

            case "Medical Report":
            {
                // Group 1: Medical / clinical / report context
                var hasReportContext = ContainsAny(textLower,
                    "medical", "clinical", "report", "examination", "lab report",
                    "test report", "hospital", "clinic", "pathology", "radiology",
                    "investigation", "laboratory");

                // Group 2: Patient context
                var hasPatientContext = ContainsAny(textLower,
                    "patient", "patient name", "patient id", "dob", "date of birth",
                    "age", "admitted", "history");

                // Group 3: Physician / Provider context
                var hasProviderContext = ContainsAny(textLower,
                    "doctor", "physician", "dr.", "dr ", "consultant", "attending physician",
                    "surgeon", "specialist", "practitioner");

                // Group 4: Diagnosis / clinical findings / assessment / treatment
                var hasClinicalFindings = ContainsAny(textLower,
                    "diagnosis", "clinical diagnosis", "findings", "clinical findings",
                    "assessment", "treatment", "treatment plan", "symptoms", "condition",
                    "impression", "complaint", "injury", "prescribed");

                int distinctGroups = 0;
                if (hasReportContext) distinctGroups++;
                if (hasPatientContext) distinctGroups++;
                if (hasProviderContext) distinctGroups++;
                if (hasClinicalFindings) distinctGroups++;

                // Must have clinical findings/diagnosis/treatment AND multiple groups
                return (distinctGroups >= 3 && hasClinicalFindings) ||
                       (hasReportContext && hasProviderContext && hasClinicalFindings) ||
                       (distinctGroups >= 2 && hasClinicalFindings && (hasReportContext || hasPatientContext));
            }

            case "Hospital Bills":
            {
                // Group 1: Billing evidence
                var hasBillingEvidence = ContainsAny(textLower,
                    "invoice", "bill", "statement", "charges", "fee", "receipt",
                    "payment", "billing", "account", "room charges", "admission fee",
                    "pharmacy charges", "discharge summary", "itemized");

                // Group 2: Facility / Provider
                var hasFacility = ContainsAny(textLower,
                    "hospital", "clinic", "medical center", "healthcare", "pharmacy",
                    "dispensary", "laboratory", "nawaloka", "medical");

                // Group 3: Financial amounts / identifiers
                var hasAmounts = ContainsAny(textLower,
                    "total", "subtotal", "amount", "tax", "balance", "paid",
                    "due", "payment status", "invoice no", "bill no", "invoice date",
                    "bill date", "lkr", "rs.", "usd", "$", "account no", "patient id");

                return hasBillingEvidence && hasAmounts && (hasFacility || ContainsAny(textLower, "patient", "dr.", "doctor"));
            }

            case "Prescription":
            {
                // Group 1: Prescribing / Rx context
                var hasRxContext = ContainsAny(textLower,
                    "prescription", "prescribed", "rx", "dispensed", "dispense",
                    "directions", "instructions", "take", "medicines", "medication", "sig");

                // Group 2: Medication dosage / form
                var hasDosage = ContainsAny(textLower,
                    "dosage", "dose", "tablet", "tablets", "capsule", "capsules",
                    "mg", "ml", "syrup", "ointment", "drops", "daily", "times daily",
                    "every 8 hours", "every 6 hours", "od", "bd", "tid", "qid",
                    "stat", "prn", "paracetamol", "ibuprofen", "amoxicillin");

                // Group 3: Provider / Patient
                var hasProviderOrPatient = ContainsAny(textLower,
                    "doctor", "physician", "dr.", "dr ", "prescribed by", "patient",
                    "patient name", "patient id", "clinic", "hospital", "pharmacy");

                return (hasRxContext || hasDosage) && hasProviderOrPatient;
            }

            case "Police Report":
            {
                var p = ContainsAny(textLower, "police", "station", "officer", "collision", "fir", "general diary", "gd entry", "law enforcement", "constable", "patrol", "traffic police", "traffic accident", "motor vehicle accident");
                var sCount = CountMatches(textLower, "accident", "incident", "report", "investigation", "witness", "damage", "vehicle");
                return p || sCount >= 2;
            }

            case "Repair Estimate":
            {
                var p = ContainsAny(textLower, "repair", "estimate", "labour", "labor", "parts", "workshop", "garage", "quotation", "contractor", "body shop", "mechanic", "body repair", "automotive");
                var s = ContainsAny(textLower, "total", "replacement", "cost", "materials", "subtotal", "tax", "hours", "estimated");
                return p && (s || CountMatches(textLower, "repair", "estimate", "parts", "labor", "labour", "workshop") >= 2);
            }

            case "Driver License":
            {
                return ContainsAny(textLower, "driver", "driving", "licence", "license", "permit", "class of vehicle", "driving licence", "driver license");
            }

            case "Death Certificate":
            {
                return ContainsAny(textLower, "death", "deceased", "cause of death", "coroner", "died", "burial", "death certificate", "certify the death");
            }

            case "Beneficiary / Nominee Identification":
            {
                var p = ContainsAny(textLower, "beneficiary", "nominee", "relationship to insured", "nominee identification", "beneficiary identification", "kin", "spouse");
                var s = ContainsAny(textLower, "identification", "identity", "national id", "passport", "full name", "nic");
                return p || (s && (textLower.Contains("beneficiary") || textLower.Contains("nominee")));
            }

            case "Policy Document":
            {
                var p = ContainsAny(textLower, "policy schedule", "policy document", "policyholder", "sum assured", "premium payable", "terms and conditions");
                var sCount = CountMatches(textLower, "policy", "coverage", "insured", "insurance", "underwriter");
                return p || sCount >= 2;
            }

            case "Claim Form":
            {
                var p = ContainsAny(textLower, "claim form", "claimant declaration", "signature of claimant", "claim details");
                var sCount = CountMatches(textLower, "claimant", "declaration", "policy number", "loss");
                return p || sCount >= 2;
            }

            case "Property Deed":
            {
                var p = ContainsAny(textLower, "title deed", "property deed", "conveyance", "land registry", "cadastral", "parcel number");
                var sCount = CountMatches(textLower, "deed", "property", "owner", "ownership", "land");
                return p || (sCount >= 2 && (textLower.Contains("deed") || textLower.Contains("title")));
            }

            case "Property Valuation":
            {
                var p = ContainsAny(textLower, "valuation report", "property valuation", "appraisal", "surveyor", "market valuation");
                var sCount = CountMatches(textLower, "valuation", "property", "assessed value", "replacement cost");
                return p || sCount >= 2;
            }

            case "Travel Itinerary":
            {
                var p = ContainsAny(textLower, "flight", "boarding pass", "e-ticket", "airline", "passenger", "booking reference", "itinerary");
                var sCount = CountMatches(textLower, "travel", "departure", "arrival", "hotel", "reservation");
                return p || sCount >= 2;
            }

            default:
                // Types without special rules (e.g. Supporting Document) accept any non-empty content
                return true;
        }
    }

    public static (bool IsMismatch, string? ResemblesType, string Reason) EvaluateContentConsistency(
        string text, string claimedType, string fileName = "")
    {
        var normClaimed = NormalizeDocumentType(claimedType);
        // Photos of Damage are image files — format compatibility is checked by CheckFormatCompatibility.
        // Text-based content consistency is not applicable to image document types.
        if (normClaimed == "Photos of Damage")
        {
            return (false, null, "Photos of Damage format compatibility validated separately via file signature check.");
        }

        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 30)
        {
            // Weak signal from filename if content couldn't be parsed
            return (false, null, "Insufficient extracted text for conclusive mismatch determination.");
        }

        var textLower = text.ToLowerInvariant();
        var fileNameLower = (fileName ?? string.Empty).ToLowerInvariant();

        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var primaryMatches = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (category, (primaryKws, secondaryKws)) in DocumentSignals)
        {
            var pList = primaryKws.Where(k => textLower.Contains(k)).ToList();
            var sList = secondaryKws.Where(k => textLower.Contains(k)).ToList();
            scores[category] = (pList.Count * 2) + sList.Count;
            primaryMatches[category] = pList;
        }

        var claimedScore = scores.TryGetValue(normClaimed, out var cs) ? cs : 0;
        var otherScores = scores.Where(kv => !string.Equals(kv.Key, normClaimed, StringComparison.OrdinalIgnoreCase)).ToList();

        string? topOtherType = null;
        int topOtherScore = 0;
        List<string>? topPrimaryList = null;
        bool hasTopOtherPrimary = false;

        if (otherScores.Count > 0)
        {
            var topOther = otherScores.OrderByDescending(kv => kv.Value).First();
            topOtherType = topOther.Key;
            topOtherScore = topOther.Value;
            hasTopOtherPrimary = primaryMatches.TryGetValue(topOtherType, out topPrimaryList) && topPrimaryList.Count > 0;
        }

        bool filenameSuggestsOther = false;
        if (topOtherType is "Beneficiary / Nominee Identification" or "Death Certificate")
        {
            if (fileNameLower.Contains("beneficiary") || fileNameLower.Contains("nominee") || fileNameLower.Contains("death_cert"))
            {
                filenameSuggestsOther = true;
            }
        }

        // 1. Evaluate whether the document satisfies the semantic evidence rules for declared category
        var isDeclaredCategoryValid = CheckCategoryValidity(normClaimed, textLower);

        if (!isDeclaredCategoryValid)
        {
            // If it strongly resembles another category, report the resembling category
            if (topOtherScore >= 3 && hasTopOtherPrimary && topOtherType != null)
            {
                var detected = topPrimaryList != null && topPrimaryList.Count > 0
                    ? string.Join(", ", topPrimaryList.Take(3))
                    : "unrelated content indicators";
                var reason = $"Uploaded file under '{normClaimed}' does not contain expected indicators and strongly resembles '{topOtherType}' (detected: {detected}).";
                return (true, topOtherType, reason);
            }

            if (filenameSuggestsOther && topOtherScore >= 2 && topOtherType != null)
            {
                var detected = topPrimaryList != null && topPrimaryList.Count > 0
                    ? string.Join(", ", topPrimaryList.Take(3))
                    : "unrelated content indicators";
                var reason = $"Uploaded file under '{normClaimed}' does not contain expected indicators and strongly resembles '{topOtherType}' (detected: {detected}).";
                return (true, topOtherType, reason);
            }

            // Unrelated content (e.g. software architecture slides, recipes, novels)
            return (true, null, $"The document content does not appear consistent with {normClaimed}.");
        }

        // 2. Even if declared category has some matches, check if another category overwhelmingly dominates
        bool isCrossTypeMismatch = false;
        if (claimedScore <= 1 && topOtherScore >= 4 && hasTopOtherPrimary && topOtherType != null)
        {
            isCrossTypeMismatch = true;
        }

        if (isCrossTypeMismatch && topOtherType != null)
        {
            var detected = topPrimaryList != null && topPrimaryList.Count > 0
                ? string.Join(", ", topPrimaryList.Take(3))
                : "unrelated content indicators";
            var reason = $"Uploaded file under '{normClaimed}' does not contain expected indicators and strongly resembles '{topOtherType}' (detected: {detected}).";
            return (true, topOtherType, reason);
        }

        return (false, null, $"Document content is consistent with '{normClaimed}'.");
    }

    public static DocumentEvaluationResult EvaluateClaimDocuments(
        string claimType,
        IEnumerable<ClaimDocument> documents,
        Func<string, byte[]?> fileByteProvider)
    {
        var docList = documents?.ToList() ?? new List<ClaimDocument>();
        var findings = new List<DocumentIntegrityFinding>();
        var hashes = new Dictionary<string, List<string>>(); // hash -> document types

        foreach (var doc in docList)
        {
            var normType = NormalizeDocumentType(doc.DocumentType);
            var bytes = fileByteProvider(doc.FileUrl);

            // 1. File size / readability check
            if (bytes == null || bytes.Length == 0)
            {
                if (!IsFormatCompatibleWithExtension(doc.FileName, normType))
                {
                    var allowedList = AllowedExtensions.TryGetValue(normType, out var exts) ? string.Join(", ", exts) : "";
                    findings.Add(new DocumentIntegrityFinding(
                        doc.Id,
                        normType,
                        doc.FileName,
                        FraudFlagType.DocumentTypeMismatch,
                        FlagSeverity.High,
                        $"File type is not valid for '{normType}'. Accepted formats: {allowedList}.",
                        DocumentVerificationStatus.Rejected
                    ));
                    continue;
                }

                if (doc.FileSize == 0)
                {
                    findings.Add(new DocumentIntegrityFinding(
                        doc.Id,
                        normType,
                        doc.FileName,
                        FraudFlagType.DocumentUnreadable,
                        FlagSeverity.High,
                        $"Document '{doc.FileName}' uploaded under '{normType}' is empty (0 bytes).",
                        DocumentVerificationStatus.Unreadable
                    ));
                    continue;
                }
            }

            if (bytes != null && bytes.Length > 0)
            {
                // 2. Hash computation and duplicate reuse tracking
                var hash = ComputeSha256(bytes);
                if (!hashes.TryGetValue(hash, out var typeList))
                {
                    typeList = new List<string>();
                    hashes[hash] = typeList;
                }
                typeList.Add(normType);

                // 3. File signature check
                var sig = DetectFileSignature(bytes);
                var ext = Path.GetExtension(doc.FileName).ToLowerInvariant();

                if (ext == ".pdf" && sig != "pdf")
                {
                    findings.Add(new DocumentIntegrityFinding(
                        doc.Id,
                        normType,
                        doc.FileName,
                        FraudFlagType.DocumentUnreadable,
                        FlagSeverity.High,
                        $"File '{doc.FileName}' has .pdf extension but lacks a valid PDF header signature.",
                        DocumentVerificationStatus.Unreadable
                    ));
                    continue;
                }

                // 3b. Format compatibility check — reject files whose actual signature
                //     is incompatible with the declared DocumentType (e.g., DOCX as "Photos of Damage")
                var (isCompatible, compatReason) = CheckFormatCompatibility(sig, normType);
                if (!isCompatible)
                {
                    findings.Add(new DocumentIntegrityFinding(
                        doc.Id,
                        normType,
                        doc.FileName,
                        FraudFlagType.DocumentTypeMismatch,
                        FlagSeverity.High,
                        compatReason,
                        DocumentVerificationStatus.Rejected
                    ));
                    continue;
                }

                // 4. Content extraction and consistency check
                var text = ExtractTextSafely(bytes, doc.FileName);

                // Scanned / non-text PDF safety: If it's a PDF and text cannot be extracted or is too short (< 30 chars),
                // it cannot be deterministically verified. Mark Unreadable / Needs Review.
                if (sig == "pdf" || ext == ".pdf")
                {
                    if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 30)
                    {
                        findings.Add(new DocumentIntegrityFinding(
                            doc.Id,
                            normType,
                            doc.FileName,
                            FraudFlagType.DocumentUnreadable,
                            FlagSeverity.High,
                            $"Document '{doc.FileName}' uploaded under '{normType}' contains insufficient or unreadable text (scanned or image-only PDF requires review).",
                            DocumentVerificationStatus.Unreadable
                        ));
                        continue;
                    }
                }

                if (!string.IsNullOrWhiteSpace(text) && text.Trim().Length >= 30)
                {
                    var (isMismatch, resemblesType, reason) = EvaluateContentConsistency(text, normType, doc.FileName);
                    if (isMismatch)
                    {
                        findings.Add(new DocumentIntegrityFinding(
                            doc.Id,
                            normType,
                            doc.FileName,
                            FraudFlagType.DocumentTypeMismatch,
                            FlagSeverity.High,
                            reason,
                            DocumentVerificationStatus.Mismatch
                        ));
                        continue;
                    }
                }
            }

            // Also check if document was already marked Mismatch or Unreadable in DB (when bytes unavailable)
            if (bytes == null || bytes.Length == 0)
            {
                if (doc.VerificationStatus == DocumentVerificationStatus.Mismatch)
                {
                    findings.Add(new DocumentIntegrityFinding(
                        doc.Id,
                        normType,
                        doc.FileName,
                        FraudFlagType.DocumentTypeMismatch,
                        FlagSeverity.High,
                        $"Document '{doc.FileName}' uploaded under '{normType}' was flagged as a document type mismatch.",
                        DocumentVerificationStatus.Mismatch
                    ));
                }
                else if (doc.VerificationStatus == DocumentVerificationStatus.Unreadable)
                {
                    findings.Add(new DocumentIntegrityFinding(
                        doc.Id,
                        normType,
                        doc.FileName,
                        FraudFlagType.DocumentUnreadable,
                        FlagSeverity.High,
                        $"Document '{doc.FileName}' uploaded under '{normType}' is unreadable.",
                        DocumentVerificationStatus.Unreadable
                    ));
                }
                else if (doc.VerificationStatus == DocumentVerificationStatus.Rejected)
                {
                    findings.Add(new DocumentIntegrityFinding(
                        doc.Id,
                        normType,
                        doc.FileName,
                        FraudFlagType.DocumentTypeMismatch,
                        FlagSeverity.High,
                        $"Document '{doc.FileName}' uploaded under '{normType}' was rejected due to incompatibility.",
                        DocumentVerificationStatus.Rejected
                    ));
                }
            }
        }

        // 5. Detect duplicate file reuse across distinct document types
        foreach (var (h, typeList) in hashes)
        {
            var distinctTypes = typeList.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (distinctTypes.Count > 1)
            {
                var docMatch = docList.FirstOrDefault(d => distinctTypes.Contains(NormalizeDocumentType(d.DocumentType)));
                findings.Add(new DocumentIntegrityFinding(
                    docMatch?.Id ?? Guid.Empty,
                    string.Join(", ", distinctTypes),
                    docMatch?.FileName ?? "unknown",
                    FraudFlagType.DuplicateDocumentReused,
                    FlagSeverity.High,
                    $"Duplicate document reuse: identical physical file content was uploaded across multiple distinct document types ({string.Join(", ", distinctTypes)}).",
                    DocumentVerificationStatus.Flagged
                ));
            }
        }

        return new DocumentEvaluationResult(findings);
    }
}

public record DocumentIntegrityFinding(
    Guid DocumentId,
    string DocumentType,
    string FileName,
    FraudFlagType FlagType,
    FlagSeverity Severity,
    string Description,
    DocumentVerificationStatus Status
);

public class DocumentEvaluationResult
{
    public List<DocumentIntegrityFinding> Findings { get; }

    public DocumentEvaluationResult(List<DocumentIntegrityFinding> findings)
    {
        Findings = findings ?? new List<DocumentIntegrityFinding>();
    }

    public bool HasMismatches => Findings.Any(f => f.FlagType == FraudFlagType.DocumentTypeMismatch);
    public bool HasUnreadable => Findings.Any(f => f.FlagType == FraudFlagType.DocumentUnreadable);
    public bool HasDuplicates => Findings.Any(f => f.FlagType == FraudFlagType.DuplicateDocumentReused);
    public bool HasAnyIssues => Findings.Count > 0;
}
