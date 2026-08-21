import React, { useState, useEffect } from 'react';
import { Globe } from 'lucide-react';
import Analytics from '@/lib/analytics';
import { toast } from 'sonner';
import { useConfig } from '@/contexts/ConfigContext';

export interface Language {
  code: string;
  name: string;
}

// ISO 639-1 language codes supported by Whisper
const LANGUAGES: Language[] = [
  { code: 'auto', name: 'اكتشاف تلقائي (اللغة الأصلية)' },
  { code: 'auto-translate', name: 'اكتشاف تلقائي (الترجمة إلى الإنجليزية)' },
  { code: 'en', name: 'الإنجليزية' }, { code: 'zh', name: 'الصينية' },
  { code: 'de', name: 'الألمانية' }, { code: 'es', name: 'الإسبانية' },
  { code: 'ru', name: 'الروسية' }, { code: 'ko', name: 'الكورية' },
  { code: 'fr', name: 'الفرنسية' }, { code: 'ja', name: 'اليابانية' },
  { code: 'pt', name: 'البرتغالية' }, { code: 'tr', name: 'التركية' },
  { code: 'pl', name: 'البولندية' }, { code: 'ca', name: 'الكتالونية' },
  { code: 'nl', name: 'الهولندية' }, { code: 'ar', name: 'العربية' },
  { code: 'sv', name: 'السويدية' }, { code: 'it', name: 'الإيطالية' },
  { code: 'id', name: 'الإندونيسية' }, { code: 'hi', name: 'الهندية' },
  { code: 'fi', name: 'الفنلندية' }, { code: 'vi', name: 'الفيتنامية' },
  { code: 'he', name: 'العبرية' }, { code: 'uk', name: 'الأوكرانية' },
  { code: 'el', name: 'اليونانية' }, { code: 'ms', name: 'الماليزية' },
  { code: 'cs', name: 'التشيكية' }, { code: 'ro', name: 'الرومانية' },
  { code: 'da', name: 'الدنماركية' }, { code: 'hu', name: 'المجرية' },
  { code: 'ta', name: 'التاميلية' }, { code: 'no', name: 'النرويجية' },
  { code: 'th', name: 'التايلاندية' }, { code: 'ur', name: 'الأردية' },
  { code: 'hr', name: 'الكرواتية' }, { code: 'bg', name: 'البلغارية' },
  { code: 'lt', name: 'الليتوانية' }, { code: 'la', name: 'اللاتينية' },
  { code: 'mi', name: 'الماورية' }, { code: 'ml', name: 'المالايالامية' },
  { code: 'cy', name: 'الويلزية' }, { code: 'sk', name: 'السلوفاكية' },
  { code: 'te', name: 'التيلوغوية' }, { code: 'fa', name: 'الفارسية' },
  { code: 'lv', name: 'اللاتفية' }, { code: 'bn', name: 'البنغالية' },
  { code: 'sr', name: 'الصربية' }, { code: 'az', name: 'الأذربيجانية' },
  { code: 'sl', name: 'السلوفينية' }, { code: 'kn', name: 'الكنادية' },
  { code: 'et', name: 'الإستونية' }, { code: 'mk', name: 'المقدونية' },
  { code: 'br', name: 'البريتونية' }, { code: 'eu', name: 'الباسكية' },
  { code: 'is', name: 'الأيسلندية' }, { code: 'hy', name: 'الأرمنية' },
  { code: 'ne', name: 'النيبالية' }, { code: 'mn', name: 'المنغولية' },
  { code: 'bs', name: 'البوسنية' }, { code: 'kk', name: 'الكازاخية' },
  { code: 'sq', name: 'الألبانية' }, { code: 'sw', name: 'السواحيلية' },
  { code: 'gl', name: 'الجليقية' }, { code: 'mr', name: 'الماراثية' },
  { code: 'pa', name: 'البنجابية' }, { code: 'si', name: 'السنهالية' },
  { code: 'km', name: 'الخميرية' }, { code: 'sn', name: 'الشونا' },
  { code: 'yo', name: 'اليوروبا' }, { code: 'so', name: 'الصومالية' },
  { code: 'af', name: 'الأفريقانية' }, { code: 'oc', name: 'الأوكسيتانية' },
  { code: 'ka', name: 'الجورجية' }, { code: 'be', name: 'البيلاروسية' },
  { code: 'tg', name: 'الطاجيكية' }, { code: 'sd', name: 'السندية' },
  { code: 'gu', name: 'الغوجاراتية' }, { code: 'am', name: 'الأمهرية' },
  { code: 'yi', name: 'اليديشية' }, { code: 'lo', name: 'اللاوية' },
  { code: 'uz', name: 'الأوزبكية' }, { code: 'fo', name: 'الفاروية' },
  { code: 'ht', name: 'الكريولية الهايتية' }, { code: 'ps', name: 'البشتوية' },
  { code: 'tk', name: 'التركمانية' }, { code: 'nn', name: 'النرويجية النينوشكية' },
  { code: 'mt', name: 'المالطية' }, { code: 'sa', name: 'السنسكريتية' },
  { code: 'lb', name: 'اللوكسمبورغية' }, { code: 'my', name: 'البورمية' },
  { code: 'bo', name: 'التبتية' }, { code: 'tl', name: 'التاغالوغية' },
  { code: 'mg', name: 'الملغاشية' }, { code: 'as', name: 'الأسامية' },
  { code: 'tt', name: 'التترية' }, { code: 'haw', name: 'الهاوائية' },
  { code: 'ln', name: 'اللينغالية' }, { code: 'ha', name: 'الهوسا' },
  { code: 'ba', name: 'الباشكيرية' }, { code: 'jw', name: 'الجاوية' },
  { code: 'su', name: 'السوندية' },
];

interface LanguageSelectionProps {
  selectedLanguage: string;
  onLanguageChange: (language: string) => void;
  disabled?: boolean;
  provider?: 'localWhisper' | 'deepgram' | 'elevenLabs' | 'groq' | 'openai';
}

export function LanguageSelection({
  selectedLanguage,
  onLanguageChange,
  disabled = false,
  provider = 'localWhisper'
}: LanguageSelectionProps) {
  const [saving, setSaving] = useState(false);
  const { setSelectedLanguage } = useConfig();

  const availableLanguages = LANGUAGES;

  const handleLanguageChange = async (languageCode: string) => {
    setSaving(true);
    try {
      // Save language preference to localStorage and sync to backend
      setSelectedLanguage(languageCode);
      onLanguageChange(languageCode);
      console.log('Language preference saved:', languageCode);

      // Track language selection analytics
      const selectedLang = LANGUAGES.find(lang => lang.code === languageCode);
      await Analytics.track('language_selected', {
        language_code: languageCode,
        language_name: selectedLang?.name || 'Unknown',
        is_auto_detect: (languageCode === 'auto').toString(),
        is_auto_translate: (languageCode === 'auto-translate').toString()
      });

      // Show success toast
      const languageName = selectedLang?.name || languageCode;
      toast.success("Language preference saved", {
        description: `Transcription language set to ${languageName}`
      });
    } catch (error) {
      console.error('Failed to save language preference:', error);
      toast.error("Failed to save language preference", {
        description: error instanceof Error ? error.message : String(error)
      });
    } finally {
      setSaving(false);
    }
  };

  // Find the selected language name for display
  const selectedLanguageName = LANGUAGES.find(
    lang => lang.code === selectedLanguage
  )?.name || 'اكتشاف تلقائي (اللغة الأصلية)';

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Globe className="h-4 w-4 text-gray-600" />
          <h4 className="text-sm font-medium text-gray-900">لغة التفريغ</h4>
        </div>
      </div>

      <div className="space-y-2">
        <select
          value={selectedLanguage}
          onChange={(e) => handleLanguageChange(e.target.value)}
          disabled={disabled || saving}
          className="w-full px-3 py-2 text-sm bg-white border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-1 focus:ring-blue-500 focus:border-blue-500 disabled:bg-gray-50 disabled:text-gray-500"
        >
          {availableLanguages.map((language) => (
            <option key={language.code} value={language.code}>
              {language.name}
              {language.code !== 'auto' && language.code !== 'auto-translate' && ` (${language.code})`}
            </option>
          ))}
        </select>

        {/* Info text */}
        <div className="text-xs space-y-2 pt-2">
          <p className="text-gray-600">
            <strong>الحالية:</strong> {selectedLanguageName}
          </p>
          {selectedLanguage === 'auto' && (
            <div className="p-2 bg-yellow-50 border border-yellow-200 rounded text-yellow-800">
              <p className="font-medium">⚠️ Auto Detect may produce incorrect results</p>
              <p className="mt-1">لأفضل دقة، اختر اللغة المستخدمة في التسجيل.</p>
            </div>
          )}
          {selectedLanguage === 'auto-translate' && (
            <div className="p-2 bg-blue-50 border border-blue-200 rounded text-blue-800">
              <p className="font-medium">🌐 Translation Mode Active</p>
              <p className="mt-1">ستُترجم المقاطع الصوتية تلقائيًا إلى الإنجليزية عند اختيار وضع الترجمة.</p>
            </div>
          )}
          {selectedLanguage !== 'auto' && selectedLanguage !== 'auto-translate' && (
            <p className="text-gray-600">
              Transcription will be optimized for <strong>{selectedLanguageName}</strong>
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
