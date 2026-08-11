import { useEffect, useState } from 'react'
import { api } from '../../api/gymshop'
import type { Product } from '../../api/types'
import { ProductImage } from '../catalog/ProductImage'

const money = (value: number) => new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' }).format(value)
const hasSupportedImage = (product: Product) => Boolean(product.imageUrl && (product.imageUrl.startsWith('/') || /^https?:\/\//i.test(product.imageUrl)))

export function Home({ onCatalog, onProduct }: { onCatalog(): void; onProduct(id: number): void }) {
  const [products, setProducts] = useState<Product[]>([])
  const [selected, setSelected] = useState<Product | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)

  useEffect(() => {
    setLoadError(false)
    api.products(false).then(result => {
      const ordered = [...result.filter(hasSupportedImage), ...result.filter(product => !hasSupportedImage(product))]
      setProducts(ordered.slice(0, 6))
    }).catch(() => setLoadError(true)).finally(() => setLoading(false))
  }, [])

  const heroProduct = products.find(product => product.name.toLocaleLowerCase('es').includes('mancuerna')) ?? products[0]
  const campaignProduct = products.find(product => product.name.toLocaleLowerCase('es').includes('kettlebell'))
  return <>
    <section className="home-hero">
      <img src="/images/home/hero-training.webp" alt="Atleta entrenando con mancuernas" />
      <div className="home-hero-overlay">
        <p className="eyebrow">EQUIPÁ TU MEJOR VERSIÓN</p>
        <h1>Entrená sin límites.</h1>
        <p>Equipamiento seleccionado para construir fuerza, constancia y resultados.</p>
        <button className="primary hero-cta" onClick={onCatalog}>Ver catálogo</button>
      </div>
      {heroProduct && <button className="hero-product-link" onClick={() => onProduct(heroProduct.id)}>Ver {heroProduct.name} <span>→</span></button>}
    </section>

    <section className="featured-products">
      <div className="section-title"><div><p className="eyebrow">SELECCIÓN GYMSHOP</p><h2>Productos destacados</h2></div><button onClick={onCatalog}>Ver todos</button></div>
      {loading ? <div className="empty">Cargando productos destacados…</div> : loadError ? <div className="empty">No pudimos cargar los destacados.</div> : products.length === 0 ? <div className="empty">Todavía no hay productos destacados.</div> : <div className="product-grid">{products.map(product => <article className="product-card featured-card" key={product.id}>
        <button className="product-image" onClick={() => setSelected(product)}><ProductImage src={product.imageUrl} alt={product.name} /></button>
        <div><small>{product.stock > 0 ? `${product.stock} disponibles` : 'Sin stock'}</small><h3>{product.name}</h3><p>{product.description}</p><div className="price-row"><strong>{money(product.price)}</strong><button onClick={() => setSelected(product)}>Ver producto</button></div></div>
      </article>)}</div>}
    </section>

    {campaignProduct && <section className="editorial-section" aria-label="Producto en acción">
      <button className="editorial-banner" onClick={() => onProduct(campaignProduct.id)}>
        <img src="/images/home/lifestyle-kettlebell.webp" alt="Atleta entrenando con una kettlebell" loading="lazy" />
        <span className="editorial-copy">
          <span className="eyebrow">FUERZA QUE SE SIENTE</span>
          <strong>Entrená fuerte.<br />Donde quieras.</strong>
          <span>Descubrí la {campaignProduct.name} <b>→</b></span>
        </span>
      </button>
    </section>}

    <section className="trust-section" aria-label="Beneficios de comprar en GymShop">
      <article><span>AR</span><div><h3>Envíos a todo el país</h3><p>Recibí tu equipamiento estés donde estés.</p></div></article>
      <article><span>✓</span><div><h3>Compra protegida</h3><p>Tu cuenta y tus órdenes siempre bajo control.</p></div></article>
      <article><span>$</span><div><h3>Opciones de pago</h3><p>Elegí la alternativa disponible que mejor te resulte.</p></div></article>
    </section>

    {selected && <div className="modal" role="dialog" aria-modal="true" aria-label={`Detalle de ${selected.name}`}><div className="modal-card"><button className="close" aria-label="Cerrar detalle" onClick={() => setSelected(null)}>×</button><ProductImage src={selected.imageUrl} alt={selected.name} /><p className="eyebrow">PRODUCTO DESTACADO</p><h2>{selected.name}</h2><p>{selected.description || 'Equipamiento GymShop.'}</p><p><strong>{money(selected.price)}</strong> · Stock {selected.stock}</p><button className="primary" onClick={() => onProduct(selected.id)}>Ver producto</button></div></div>}
  </>
}
