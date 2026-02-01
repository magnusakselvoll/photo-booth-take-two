export const translations = {
  en: {
    // Download page
    downloadYourPhoto: 'Download Your Photo',
    enterPhotoCode: 'Enter your photo code',
    searching: 'Searching...',
    findPhoto: 'Find Photo',
    photoNotFound: 'Photo not found. Please check your code.',
    downloadPhoto: 'Download Photo',
    backToSearch: 'Back to Search',
    orBrowseAllPhotos: 'or browse all photos',

    // Photo grid
    loadingPhotos: 'Loading photos...',
    failedToLoadPhotos: 'Failed to load photos',
    noPhotosYet: 'No photos yet',

    // Photo detail page
    loading: 'Loading...',
    photoNotFoundError: 'Photo not found',
    backToGallery: 'Back to Gallery',
    code: 'Code',

    // Booth page
    tapToTakePhoto: 'Tap anywhere to take a photo',

    // Slideshow
    noPhotosToShow: 'No photos to show yet',
  },
  es: {
    // Download page
    downloadYourPhoto: 'Descarga Tu Foto',
    enterPhotoCode: 'Ingresa el código de tu foto',
    searching: 'Buscando...',
    findPhoto: 'Buscar Foto',
    photoNotFound: 'Foto no encontrada. Por favor verifica el código.',
    downloadPhoto: 'Descargar Foto',
    backToSearch: 'Volver a Buscar',
    orBrowseAllPhotos: 'o explora todas las fotos',

    // Photo grid
    loadingPhotos: 'Cargando fotos...',
    failedToLoadPhotos: 'Error al cargar las fotos',
    noPhotosYet: 'Aún no hay fotos',

    // Photo detail page
    loading: 'Cargando...',
    photoNotFoundError: 'Foto no encontrada',
    backToGallery: 'Volver a la Galería',
    code: 'Código',

    // Booth page
    tapToTakePhoto: 'Toca en cualquier lugar para tomar una foto',

    // Slideshow
    noPhotosToShow: 'Aún no hay fotos para mostrar',
  },
} as const;

export type Language = keyof typeof translations;
export type TranslationKey = keyof typeof translations.en;
