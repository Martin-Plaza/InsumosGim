import type { CreateProductInput, Product, UpdateProductInput } from '../../api/types'

export const PRODUCT_LIMITS = { name: 150, description: 1000, imageUrl: 500, maxPrice: Number('9999999999999999.99') } as const

export interface ProductFormValues {
  name: string
  description: string
  price: string
  stock: string
  categoryId: string
  imageUrl: string
  isActive: boolean
}

export type ProductField = keyof Omit<ProductFormValues, 'isActive'>
export type ProductFormErrors = Partial<Record<ProductField, string>>

export const emptyProductValues = (): ProductFormValues => ({ name: '', description: '', price: '', stock: '', categoryId: '', imageUrl: '', isActive: true })

export const productToFormValues = (product: Product): ProductFormValues => ({
  name: product.name,
  description: product.description || '',
  price: String(product.price),
  stock: String(product.stock),
  categoryId: product.category ? String(product.category.id) : '',
  imageUrl: product.imageUrl || '',
  isActive: product.isActive,
})

export function isValidProductImageUrl(value: string) {
  const url = value.trim()
  if (!url) return true
  if (url.startsWith('/') && !url.startsWith('//') && !url.includes('..')) return true
  try { const parsed = new URL(url); return parsed.protocol === 'http:' || parsed.protocol === 'https:' } catch { return false }
}

export function validateProduct(values: ProductFormValues): ProductFormErrors {
  const errors: ProductFormErrors = {}
  const name = values.name.trim()
  const description = values.description.trim()
  const imageUrl = values.imageUrl.trim()
  const price = Number(values.price)
  const stock = Number(values.stock)
  if (!name) errors.name = 'El nombre es obligatorio.'
  else if (name.length > PRODUCT_LIMITS.name) errors.name = `El nombre no puede superar los ${PRODUCT_LIMITS.name} caracteres.`
  if (description.length > PRODUCT_LIMITS.description) errors.description = `La descripción no puede superar los ${PRODUCT_LIMITS.description} caracteres.`
  if (!values.price.trim() || !Number.isFinite(price) || price <= 0) errors.price = 'El precio debe ser mayor a cero.'
  else if (price > PRODUCT_LIMITS.maxPrice || !/^\d+(\.\d{1,2})?$/.test(values.price.trim())) errors.price = 'El precio debe tener hasta 16 dígitos enteros y 2 decimales.'
  if (!values.stock.trim() || !Number.isInteger(stock) || stock < 0) errors.stock = 'El stock debe ser un número entero mayor o igual a cero.'
  if (!values.categoryId) errors.categoryId = 'Seleccioná una categoría.'
  if (imageUrl.length > PRODUCT_LIMITS.imageUrl) errors.imageUrl = `La URL no puede superar los ${PRODUCT_LIMITS.imageUrl} caracteres.`
  else if (!isValidProductImageUrl(imageUrl)) errors.imageUrl = 'Ingresá una URL http/https o una ruta local que comience con “/”.'
  return errors
}

export function toProductInput(values: ProductFormValues): CreateProductInput {
  return {
    name: values.name.trim(), description: values.description.trim() || null,
    price: Number(values.price), stock: Number(values.stock),
    imageUrl: values.imageUrl.trim() || null, categoryId: Number(values.categoryId),
  }
}

export function toUpdateProductInput(values: ProductFormValues): UpdateProductInput {
  return {
    name: values.name.trim(), description: values.description.trim() || null,
    price: Number(values.price), imageUrl: values.imageUrl.trim() || null,
    categoryId: Number(values.categoryId), isActive: values.isActive,
  }
}
