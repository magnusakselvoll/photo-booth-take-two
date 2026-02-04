import { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { BoothPage } from './pages/BoothPage';
import { DownloadPage } from './pages/DownloadPage';
import { PhotoDetailPage } from './pages/PhotoDetailPage';
import { getClientConfig } from './api/client';
import './App.css';

function App() {
  const [qrCodeBaseUrl, setQrCodeBaseUrl] = useState<string | undefined>(undefined);
  const [swirlEffect, setSwirlEffect] = useState(true);

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
    <BrowserRouter>
      <div className="app">
        <Routes>
          <Route path="/" element={<BoothPage qrCodeBaseUrl={qrCodeBaseUrl} swirlEffect={swirlEffect} />} />
          <Route path="/download" element={<DownloadPage />} />
          <Route path="/photo/:code" element={<PhotoDetailPage />} />
        </Routes>
      </div>
    </BrowserRouter>
  );
}

export default App;
