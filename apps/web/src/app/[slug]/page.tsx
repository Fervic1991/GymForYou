import { redirect } from 'next/navigation';

export default async function GymSlugRootPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  redirect(`/${slug}/login`);
}
