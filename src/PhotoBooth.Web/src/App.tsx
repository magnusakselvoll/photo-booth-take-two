import { useState, useEffect } from 'react';
import { BoothPage } from './pages/BoothPage';
import { DownloadPage } from './pages/DownloadPage';
import { getClientConfig } from './api/client';
import './App.css';

type Route = 'booth' | 'download';

function getRouteFromHash(): Route {
  const hash = window.location.hash.slice(1);
  if (hash.startsWith('download')) return 'download';
  return 'booth';
}

function App() {
  const [route, setRoute] = useState<Route>(getRouteFromHash);
  const [qrCodeBaseUrl, setQrCodeBaseUrl] = useState<string | undefined>(undefined);

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
      })
      .catch(err => {
        console.error('Failed to load client config:', err);
      });
  }, []);

  return (
    <div className="app">
      {route === 'booth' && <BoothPage qrCodeBaseUrl={qrCodeBaseUrl} />}
      {route === 'download' && <DownloadPage />}
    </div>
  );
}

export default App;
