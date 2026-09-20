import { useEffect, useState } from 'react'
import { api } from '../../api/gymshop'
import type { Product } from '../../api/types'
import { ProductImage } from '../catalog/ProductImage'
import { money, storefront } from '../../config/storefront'

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
      <img src={storefront.assets.hero} alt={storefront.copy.heroImageAlt} />
      <div className="home-hero-overlay">
        <p className="eyebrow">{storefront.copy.heroEyebrow}</p>
        <h1>{storefront.copy.heroTitle}</h1>
        <p>{storefront.copy.heroDescription}</p>
        <button className="primary hero-cta" onClick={onCatalog}>{storefront.copy.heroAction}</button>
      </div>
      {heroProduct && <button className="hero-product-link" onClick={() => onProduct(heroProduct.id)}>Ver {heroProduct.name} <span>→</span></button>}
    </section>

    <section className="featured-products">
      <div className="section-title"><div><p className="eyebrow">{storefront.copy.featuredEyebrow}</p><h2>{storefront.copy.featuredTitle}</h2></div><button onClick={onCatalog}>{storefront.copy.featuredAction}</button></div>
      {loading ? <div className="empty">{storefront.copy.featuredLoading}</div> : loadError ? <div className="empty">{storefront.copy.featuredLoadError}</div> : products.length === 0 ? <div className="empty">{storefront.copy.featuredEmpty}</div> : <div className="product-grid">{products.map(product => <article className="product-card featured-card" key={product.id}>
        <button className="product-image" onClick={() => setSelected(product)}><ProductImage src={product.imageUrl} alt={product.name} /></button>
        <div><small>{product.stock > 0 ? `${product.stock} disponibles` : 'Sin stock'}</small><h3>{product.name}</h3><p>{product.description}</p><div className="price-row"><strong>{money(product.price)}</strong><button onClick={() => setSelected(product)}>{storefront.copy.productAction}</button></div></div>
      </article>)}</div>}
    </section>

    {campaignProduct && <section className="editorial-section" aria-label={storefront.copy.campaignAriaLabel}>
      <button className="editorial-banner" onClick={() => onProduct(campaignProduct.id)}>
        <img src={storefront.assets.campaign} alt={storefront.copy.campaignImageAlt} loading="lazy" />
        <span className="editorial-copy">
          <span className="eyebrow">{storefront.copy.campaignEyebrow}</span>
          <strong>{storefront.copy.campaignTitleLine1}<br />{storefront.copy.campaignTitleLine2}</strong>
          <span>{storefront.copy.campaignPrefix} {campaignProduct.name} <b>→</b></span>
        </span>
      </button>
    </section>}

    <section className="trust-section" aria-label={`Beneficios de comprar en ${storefront.identity.name}`}>
      {storefront.copy.benefits.map(benefit => <article key={benefit.title}><span>{benefit.icon}</span><div><h3>{benefit.title}</h3><p>{benefit.description}</p></div></article>)}
    </section>

    {selected && <div className="modal" role="dialog" aria-modal="true" aria-label={`Detalle de ${selected.name}`}><div className="modal-card"><button className="close" aria-label="Cerrar detalle" onClick={() => setSelected(null)}>×</button><ProductImage src={selected.imageUrl} alt={selected.name} /><p className="eyebrow">{storefront.copy.featuredProductEyebrow}</p><h2>{selected.name}</h2><p>{selected.description || storefront.copy.productFallback}</p><p><strong>{money(selected.price)}</strong> · Stock {selected.stock}</p><button className="primary" onClick={() => onProduct(selected.id)}>{storefront.copy.productAction}</button></div></div>}
  </>
}
