import React from 'react';
import { Lock, Sparkles, Cpu } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { OnboardingContainer } from '../OnboardingContainer';
import { useOnboarding } from '@/contexts/OnboardingContext';

export function WelcomeStep() {
  const { goNext } = useOnboarding();

  const features = [
    {
      icon: Lock,
      title: 'بياناتك تبقى على جهازك',
    },
    {
      icon: Sparkles,
      title: 'تلخيص عربي واستخراج أفكار مهمة',
    },
    {
      icon: Cpu,
      title: 'يعمل محليًا بدون اعتماد أساسي على السحابة',
    },
  ];

  return (
    <OnboardingContainer
      title="أهلًا بك في نَبْرَة"
      description="سجّل، فرّغ، ولخّص اجتماعاتك عربيًا على جهازك."
      step={1}
      hideProgress={true}
    >
      <div className="flex flex-col items-center space-y-10">
        {/* Divider */}
        <div className="w-16 h-px bg-gray-300" />

        {/* Features Card */}
        <div className="w-full max-w-md bg-white rounded-lg border border-gray-200 shadow-sm p-6 space-y-4">
          {features.map((feature, index) => {
            const Icon = feature.icon;
            return (
              <div key={index} className="flex items-start gap-3">
                <div className="flex-shrink-0 mt-0.5">
                  <div className="w-5 h-5 rounded-full bg-gray-100 flex items-center justify-center">
                    <Icon className="w-3 h-3 text-gray-700" />
                  </div>
                </div>
                <p className="text-sm text-gray-700 leading-relaxed">{feature.title}</p>
              </div>
            );
          })}
        </div>

        {/* CTA Section */}
        <div className="w-full max-w-xs space-y-3">
          <Button
            onClick={goNext}
            className="w-full h-11 bg-gray-900 hover:bg-gray-800 text-white"
          >
            ابدأ
          </Button>
          <p className="text-xs text-center text-gray-500">يستغرق الإعداد أقل من 3 دقائق</p>
        </div>
      </div>
    </OnboardingContainer>
  );
}
