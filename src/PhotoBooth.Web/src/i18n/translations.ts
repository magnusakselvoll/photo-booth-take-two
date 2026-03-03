export const translations = {
  en: {
    // Download page
    enterPhotoCode: 'Photo code',
    searching: 'Searching...',
    findPhoto: 'Find Photo',
    photoNotFound: 'Photo not found. Please check your code.',
    getPhoto: 'Get Photo',
    downloadPhoto: 'Download Photo',
    sharePhoto: 'Share Photo',

    // Photo grid
    loadingPhotos: 'Loading photos...',
    failedToLoadPhotos: 'Failed to load photos',
    noPhotosYet: 'No photos yet',

    // Photo detail page
    loading: 'Loading...',
    photoNotFoundError: 'Photo not found',
    backToGallery: 'Back to Gallery',

    // Booth page
    tapToTakePhoto: 'Tap anywhere to take a photo',

    // Slideshow
    noPhotosToShow: 'No photos to show yet',

    // Not found page
    pageNotFound: 'Page not found',
    goToGallery: 'Go to Gallery',
  },
  es: {
    // Download page
    enterPhotoCode: 'Código de foto',
    searching: 'Buscando...',
    findPhoto: 'Buscar Foto',
    photoNotFound: 'Foto no encontrada. Por favor verifica el código.',
    getPhoto: 'Obtener Foto',
    downloadPhoto: 'Descargar Foto',
    sharePhoto: 'Compartir Foto',

    // Photo grid
    loadingPhotos: 'Cargando fotos...',
    failedToLoadPhotos: 'Error al cargar las fotos',
    noPhotosYet: 'Aún no hay fotos',

    // Photo detail page
    loading: 'Cargando...',
    photoNotFoundError: 'Foto no encontrada',
    backToGallery: 'Volver a la Galería',

    // Booth page
    tapToTakePhoto: 'Toca en cualquier lugar para tomar una foto',

    // Slideshow
    noPhotosToShow: 'Aún no hay fotos para mostrar',

    // Not found page
    pageNotFound: 'Pagina no encontrada',
    goToGallery: 'Ir a la Galeria',
  },
} as const;

export type Language = keyof typeof translations;
export type TranslationKey = keyof typeof translations.en;
