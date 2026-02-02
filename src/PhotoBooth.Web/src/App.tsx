import { useState, useEffect } from 'react';
import { BoothPage } from './pages/BoothPage';
import { DownloadPage } from './pages/DownloadPage';
import { PhotoDetailPage } from './pages/PhotoDetailPage';
import { getClientConfig } from './api/client';
import './App.css';

type Route = { type: 'booth' } | { type: 'download' } | { type: 'photo'; code: string };

function getRouteFromHash(): Route {
  const hash = window.location.hash.slice(1);
  if (hash.startsWith('download')) return { type: 'download' };
  if (hash.startsWith('photo/')) {
    const code = hash.slice('photo/'.length);
    return { type: 'photo', code };
  }
  return { type: 'booth' };
}

function App() {
  const [route, setRoute] = useState<Route>(getRouteFromHash);
  const [qrCodeBaseUrl, setQrCodeBaseUrl] = useState<string | undefined>(undefined);
  const [swirlEffect, setSwirlEffect] = useState(true);

  useEffect(() => {
    const handleHashChange = () => {
      setRoute(getRouteFromHash());
    };

    window.addEventListener('hashchange', handleHashChange);
    return () => window.removeEventListener('hashchange', handleHashChange);
  }, []);

  useEffect(() => {
    getClientConfig()
      .then(config => {
        if (config.qrCodeBaseUrl) {
          setQrCodeBaseUrl(config.qrCodeBaseUrl);
        }
        setSwirlEffect(config.swirlEffect);
      })
      .catch(err => {
        console.error('Failed to load client config:', err);
      });
  }, []);

  return (
    <div className="app">
      {route.type === 'booth' && <BoothPage qrCodeBaseUrl={qrCodeBaseUrl} swirlEffect={swirlEffect} />}
      {route.type === 'download' && <DownloadPage />}
      {route.type === 'photo' && <PhotoDetailPage code={route.code} />}
    </div>
  );
}

export default App;
