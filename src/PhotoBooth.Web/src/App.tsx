import { useState, useEffect } from 'react';
import { BoothPage } from './pages/BoothPage';
import { DownloadPage } from './pages/DownloadPage';
import './App.css';

type Route = 'booth' | 'download';

function getRouteFromHash(): Route {
  const hash = window.location.hash.slice(1);
  if (hash === 'download') return 'download';
  return 'booth';
}

function App() {
  const [route, setRoute] = useState<Route>(getRouteFromHash);

  useEffect(() => {
    const handleHashChange = () => {
      setRoute(getRouteFromHash());
    };

    window.addEventListener('hashchange', handleHashChange);
    return () => window.removeEventListener('hashchange', handleHashChange);
  }, []);

  return (
    <div className="app">
      {route === 'booth' && <BoothPage />}
      {route === 'download' && <DownloadPage />}
    </div>
  );
}

export default App;
