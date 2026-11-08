import { getTranslations, setRequestLocale } from 'next-intl/server';
import { LiveMap } from '../../src/features/map/components/LiveMap';

export default async function HomePage({
  params
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);

  const t = await getTranslations('Index');
  
  return (
    <main className="relative w-full h-screen overflow-hidden bg-white text-black">
      {/* Harita Bileşeni */}
      <LiveMap />
      
      {/* Sol üstte duracak şık bir başlık paneli */}
      <div className="absolute top-4 left-4 z-10 bg-white/90 backdrop-blur-sm px-6 py-3 rounded-lg shadow-lg border border-zinc-200">
        <h1 className="text-xl font-bold text-zinc-800">{t('title')}</h1>
      </div>
    </main>
  );
}