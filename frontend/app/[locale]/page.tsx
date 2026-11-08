import { getTranslations, setRequestLocale } from 'next-intl/server';

export default async function HomePage({
  params
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  
  setRequestLocale(locale);

  const t = await getTranslations('Index');
  
  return (
    <main className="flex min-h-screen items-center justify-center bg-zinc-950 text-white">
      <h1 className="text-4xl font-bold">{t('title')}</h1>
    </main>
  );
}