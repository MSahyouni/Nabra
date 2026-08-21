# دراسة التحويل الاختياري إلى .NET WPF + WPF UI

## الخلاصة

التحويل ممكن تقنياً، لكنه **ليس ترقية واجهة فقط**. التطبيق الحالي يعتمد على Tauri/Rust وWebView وواجهات React ومحرر BlockNote ومنظومة التقاط/تفريغ أصلية؛ لذلك إعادة كتابته دفعة واحدة إلى WPF ترفع مخاطر فقد الميزات والاستقرار. التوصية هي إبقاء Tauri خط الإنتاج، واستخدام WPF UI تدريجياً في المكونات Windows-only أولاً.

## نتائج فحص `wpfui-main`

- المصدر المحلي يستهدف `net10.0-windows` في العينات الحديثة، ويدعم أيضاً أهدافاً أقدم في مكتباته.
- يتضمن NavigationView وFluentWindow والثيمات والإشعارات وSystem Tray وDependency Injection.
- قالب MBA الحالي يستخدم حزمة `WPF-UI 4.2.0` بالفعل، أي أن الهوية والثيم يمكن اختبارهما الآن دون رهن التطبيق كله بالهجرة.

## خريطة الاستبدال

| الحالي | البديل في WPF | مستوى المخاطرة |
|---|---|---|
| Next.js/React navigation | WPF UI NavigationView + MVVM | متوسط |
| Zustand/contexts/hooks | CommunityToolkit.Mvvm services | متوسط |
| BlockNote editor | WebView2 مستضاف مؤقتاً أو محرر WPF جديد | مرتفع جداً |
| أوامر Tauri | واجهات C# وخدمات process/IPC | مرتفع |
| Rust audio/Whisper | إبقاؤه sidecar مع gRPC/named pipes | مرتفع |
| Ollama HTTP | HttpClient في .NET | منخفض |
| Word COM bridge | دمج مباشر في خدمة STA | منخفض |
| SQLite | EF Core أو Microsoft.Data.Sqlite | متوسط |

## مسار PoC المقترح

1. إنشاء Shell فقط: نافذة RTL، الشريط الجانبي، شاشة اجتماع وهمية، وثيم Qomra والأخضر/الذهبي.
2. استضافة شاشة الملخص الحالية في WebView2 لإثبات التكامل، دون نقل محرر BlockNote.
3. وصل Ollama وWord بخدمات .NET أصلية.
4. وصل sidecar Rust للتسجيل والتفريغ عبر بروتوكول IPC موثق.
5. قياس بدء التشغيل والذاكرة ودقة RTL والوصولية وحجم الحزمة.

## شرط قرار Go/No-Go

يُعتمد التحويل الكامل فقط إذا أثبت PoC أن التسجيل المستمر وتشغيل Whisper وتحديث التفريغ ومحرر الملخص تعمل دون تراجع وظيفي، وأن تكلفة صيانة IPC أقل من قيمة توحيد المنصة. وإلا يبقى WPF UI للمثبّت والأدوات Windows-only، ويستمر Tauri كتطبيق المنتج.

