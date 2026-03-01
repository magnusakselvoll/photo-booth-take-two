export const translations = {
  en: {
    // Download page
    enterPhotoCode: 'Photo code',
    searching: 'Searching...',
    findPhoto: 'Find Photo',
    photoNotFound: 'Photo not found. Please check your code.',
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
  },
  es: {
    // Download page
    enterPhotoCode: 'Código de foto',
    searching: 'Buscando...',
    findPhoto: 'Buscar Foto',
    photoNotFound: 'Foto no encontrada. Por favor verifica el código.',
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
  },
} as const;

export type Language = keyof typeof translations;
export type TranslationKey = keyof typeof translations.en;
