import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

// Tailwind class çakışmalarını dinamik çözmek için standart shadcn yardımcısı
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}