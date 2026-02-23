import { Locale, messages } from '@/lib/i18n/messages';

export function tStatusBooking(status: string, locale: Locale) {
  const key = `status.booking.${status}`;
  return messages[locale][key] ?? status;
}

export function tSubscriptionStatus(status: string, locale: Locale) {
  const key = `status.subscription.${status}`;
  return messages[locale][key] ?? status;
}
