export function slugify(title: string): string {
  return title.toLowerCase().replace(/\s+/g, '-');
}

export function deslugify(slug: string): string {
  return slug.replace(/-/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
}
