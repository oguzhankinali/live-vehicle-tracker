'use client';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState } from 'react';

export function Providers({ children }: { children: React.ReactNode }) {
  //not: Her render'da yeni client oluşmasın diye state içinde tutuyoruz.
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 60 * 1000, // Veriler 1 dakika boyunca taze kabul edilsin
            refetchOnWindowFocus: false, // Sekme değiştirince gereksiz istek atmasın
          },
        },
      })
  );

  return (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
}