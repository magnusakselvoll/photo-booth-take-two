import { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { BoothPage } from './pages/BoothPage';
import { DownloadPage } from './pages/DownloadPage';
import { PhotoDetailPage } from './pages/PhotoDetailPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { getClientConfig } from './api/client';
import type { GamepadConfig } from './api/types';
import './App.css';

function App() {
  const [qrCodeBaseUrl, setQrCodeBaseUrl] = useState<string | undefined>(undefined);
  const [swirlEffect, setSwirlEffect] = useState(true);
  const [slideshowIntervalMs, setSlideshowIntervalMs] = useState(30000);
  const [gamepadConfig, setGamepadConfig] = useState<GamepadConfig | null>(null);
  const [watchdogTimeoutMs, setWatchdogTimeoutMs] = useState(300000);
  const [urlPrefix, setUrlPrefix] = useState<string | null>(null);

  useEffect(() => {
    getClientConfig()
      .then(config => {
        if (config.qrCodeBaseUrl) {
          setQrCodeBaseUrl(config.qrCodeBaseUrl);
        }
        setSwirlEffect(config.swirlEffect);
        setSlideshowIntervalMs(config.slideshowIntervalMs);
        setGamepadConfig(config.gamepad);
        setWatchdogTimeoutMs(config.watchdogTimeoutMs);
        setUrlPrefix(config.urlPrefix);
      })
      .catch(err => {
        console.error('Failed to load client config:', err);
        setUrlPrefix('');
      });
  }, []);

  if (urlPrefix === null) {
    return null;
  }

  return (
    <BrowserRouter>
      <div className="app">
        <Routes>
          <Route path="/" element={<BoothPage qrCodeBaseUrl={qrCodeBaseUrl} urlPrefix={urlPrefix} swirlEffect={swirlEffect} slideshowIntervalMs={slideshowIntervalMs} gamepadConfig={gamepadConfig} watchdogTimeoutMs={watchdogTimeoutMs} />} />
          <Route path={`/${urlPrefix}/download`} element={<DownloadPage urlPrefix={urlPrefix} />} />
          <Route path={`/${urlPrefix}/photo/:code`} element={<PhotoDetailPage urlPrefix={urlPrefix} />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </div>
    </BrowserRouter>
  );
}

export default App;
