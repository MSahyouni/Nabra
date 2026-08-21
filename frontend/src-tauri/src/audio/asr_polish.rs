fn replace_all(mut text: String, from: &str, to: &str) -> String {
    while text.contains(from) {
        text = text.replace(from, to);
    }
    text
}

fn normalize_asr_loanwords(text: &str) -> String {
    let mut out = text.to_string();
    let fixes = [
        ("سومارايز", "تلخيص"),
        ("سومرايز", "تلخيص"),
        ("سيمرايز", "تلخيص"),
        ("summary", "ملخص"),
        ("Summary", "ملخص"),
        ("summarize", "تلخيص"),
        ("Summarize", "تلخيص"),
        ("offline", "دون اتصال بالإنترنت"),
        ("Offline", "دون اتصال بالإنترنت"),
        ("اوفلاين", "دون اتصال بالإنترنت"),
        ("أوفلاين", "دون اتصال بالإنترنت"),
        ("online", "متصل بالإنترنت"),
        ("Online", "متصل بالإنترنت"),
        ("اونلاين", "متصل بالإنترنت"),
        ("أونلاين", "متصل بالإنترنت"),
        ("ASR", "تفريغ صوتي"),
        ("NLP", "معالجة لغة"),
        ("AI", "ذكاء اصطناعي"),
        ("لبلخص", "للتلخيص"),
        ("نبلش", "نبدأ"),
        ("مدلفون", "مايكروفون"),
        ("مايك-روفون", "مايكروفون"),
        ("مايكـروفون", "مايكروفون"),
        ("سكربتات", "سكربتات"),
        ("سكريبتات", "سكربتات"),
        ("تفريغ الناس", "تفريغ النص"),
        ("تلخيص الناس", "تلخيص النص"),
        ("شنوة", "شيء"),
        ("شنوه", "شيء"),
        ("الرزائي", "الغذائي"),
        ("الغزائي", "الغذائي"),
        ("الرذائي", "الغذائي"),
        ("دعم الغذائي", "الدعم الغذائي"),
        ("دعم الرزائي", "الدعم الغذائي"),
        ("وجبهات", "وجبات"),
        ("الثلاثة وجبات", "الثلاث وجبات"),
        ("الثلاثه وجبات", "الثلاث وجبات"),
        ("فبدايه", "فبداية"),
        ("احياناً", "أحياناً"),
        ("احيانا", "أحياناً"),
        ("ادارة الصرف", "إدارة الصرف"),
        ("منتجات مصنعه", "منتجات مصنّعة"),
        ("مصاريف زايده", "مصاريف زائدة"),
    ];

    for (from, to) in fixes {
        out = out.replace(from, to);
    }

    out
}

fn light_sentence_breaks_ar(text: &str) -> String {
    let mut out = text.to_string();
    for marker in [
        " فبداية",
        " اليوم نحن",
        " وهذا الموضوع",
        " وهذا الشيء",
        " أهلا ",
        " الحمد لله",
        " لازم ندور",
    ] {
        let replacement = format!(".{}", marker);
        out = out.replace(marker, &replacement);
    }
    out
}

fn collapse_repeated_fillers(text: &str) -> String {
    let mut out = text.to_string();
    for filler in ["يعني", "هيك", "تمام", "طيب", "مزبوط", "اوكي", "أوكي"] {
        let doubled = format!("{filler} {filler}");
        while out.contains(&doubled) {
            out = out.replace(&doubled, filler);
        }
    }
    out
}

pub fn polish_transcript_ar(text: &str) -> String {
    let mut out = text.trim().to_string();
    if out.is_empty() {
        return out;
    }

    out = normalize_asr_loanwords(&out);
    out = collapse_repeated_fillers(&out);
    out = light_sentence_breaks_ar(&out);

    for punctuation in [".", "،", ":", "؛", "؟", "!"] {
        let cramped = format!("{punctuation}");
        out = out.replace(&format!("{cramped}"), punctuation);
    }

    out = replace_all(out, "  ", " ");
    out = replace_all(out, "\n\n\n", "\n\n");
    out = out.replace(" .", ".");
    out = out.replace(" ،", "،");
    out = out.replace(" ؟", "؟");
    out.trim().to_string()
}

#[cfg(test)]
mod tests {
    use super::polish_transcript_ar;

    #[test]
    fn polishes_common_arabic_asr_terms() {
        let text = polish_transcript_ar("نبلش summary عن دعم الرزائي ووجبهات");
        assert!(text.contains("نبدأ"));
        assert!(text.contains("ملخص"));
        assert!(text.contains("الدعم الغذائي"));
        assert!(text.contains("وجبات"));
    }

    #[test]
    fn collapses_repeated_fillers() {
        assert_eq!(polish_transcript_ar("تمام تمام تمام نبدأ"), "تمام نبدأ");
    }
}
