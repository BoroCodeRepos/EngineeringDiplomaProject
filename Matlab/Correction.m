close all; clear; clc;

format long;
syms x;
% wartości zmierzone
Meas = [
    126.1832 147.6006 180.2216 196.8221 208.4288 231.3545 250.6058 270.1945 283.4323 307.1494 322.3885
] * 1E-12;

% wartości rzeczywiste
Real = [
     95.4870 116.6560 149.5600 165.7383 177.2800 199.3710 220.5100 240.7840 252.6100 276.2010 293.1200
] * 1E-12;

% błąd losowy ładowania pojemności
LosC = [
     2 2 6 22 15 11 6 20 34 15 7
] / 16E6;
LosCP = LosC ./ [0.0491    0.0599    0.0768    0.0851    0.0911    0.1024    0.1133    0.1237    0.1298    0.1419    0.1506] * 1E-1

% błąd losowy rozładowania pojemności
LosD = [
     2 2 11 13 9 8 11 32 42 28 12
] / 16E6;
LosDP = LosD ./ [0.0490    0.0598    0.0767    0.0850    0.0909    0.1023    0.1131    0.1235    0.1296    0.1417    0.1504] * 1E-1

%% wyznaczenie wzoru korekcyjnego za pomocą regresji liniowej
[a, b, Regression] = LinearRegression(Meas, Real);

%% wyznaczenie wzoru korekcyjnego za pomocą interpolacji Lagrange'a
INTERPOLATION_FULL_RANGE = 1;
%IntN = [1, 3, 5, 11];
IntN = [1, 2, 5, 11];
IntX = [Meas(IntN(1)), Meas(IntN(2)), Meas(IntN(3)), Meas(IntN(4))];
IntY = [Real(IntN(1)), Real(IntN(2)), Real(IntN(3)), Real(IntN(4))];
Poly = LagrangeInterpolation(IntX, IntY);
start = find(Meas==IntX(1)); stop = find(Meas==IntX(end));
Interpolation = polyval(Poly, Meas(start:stop));
%% wyniki pomiarowe
fprintf('Linear Regression result:\n');
fprintf('  y = %3.4e * x + %3.4e \n \n', a, b);

fprintf('Lagrange Interpolation: \n');
fprintf('  y = %3.4e x^3 + %3.4e x^2 + %3.4e x + %3.4e \n', ...
    Poly(1), Poly(2), Poly(3), Poly(4));
fprintf('  poly: [%3.4e, %3.4e, %3.4e, %3.4e] \n \n', ...
    Poly(1), Poly(2), Poly(3), Poly(4));
Poly(1) * 1E-19
Poly(2) * 1E-10
Poly(3)
Poly(4) * 1E10

figure('Name', 'Capacitance Correction');
Real = Real * 1E12; Meas = Meas * 1E12;
Regression = Regression * 1E12;

if INTERPOLATION_FULL_RANGE == 0
    %% wyznaczenie interpolacji po wskazanych punktach
    [Ierr, Ierr_abs] = Error(polyval(Poly, Meas(start:stop)* 1E-12), Real(start:stop)* 1E-12);
    [Int, Ierr, Ierr_abs] = FillVectors(0, Interpolation * 1E12, Ierr, Ierr_abs, start, stop, size(Meas, 2));
    p = plot(Meas, Real, Meas, Regression, IntX * 1E12, polyval(Poly, IntX) * 1E12);
else
    %% wyznaczenie interpolacji w całym mierzonym zakresie
    Int = polyval(Poly, Meas * 1E-12) * 1E12;
    [Ierr, Ierr_abs] = Error(Int, Real);
    Ierr = Ierr * 1E-12;
    p = plot(Meas, Real, Meas, Regression, Meas, Int);
end
p(1).Marker = 'o';
p(1).MarkerSize = 5;
p(1).LineWidth = 1.25;
p(2).LineWidth = 1.25;
p(3).LineWidth = 2;
title('Capacitance Correction');
xlabel('Measured capacity [pF]'); ylabel('Real capacity [pF]');
legend('Real capacity', 'Linear correction', 'Interpolation')

%% wyznaczenie maksymalnych błędów korekcji
[Rerr, Rerr_abs] = Error(Regression * 1E-12, Real * 1E-12);
fprintf(' ( Regression ) max error: %3.1f pF  (%3.1f %%) \n', ...
    max(Rerr * 1E12), max(Rerr_abs));
fprintf(' ( Interpolation ) max error: %3.1f pF  (%3.1f %%) \n\n', ...
    max(Ierr * 1E12), max(Ierr_abs));

err = [Rerr * 1E12; Rerr_abs; Ierr * 1E12; Ierr_abs];

DisplayErrors(Real, Rerr, Rerr_abs, 'relative');
DisplayErrors(Real, Ierr, Ierr_abs, 'interpolation');

%% wyznaczenie tablic pomiarowych
fprintf(' [real value]\t[measurement]\t[regression]\t[interpolation]\t[REG rel err]\t[INT rel err]\t[REG abs err]\t[INT abs err]\n');
for i = 1:1:size(Meas, 2)
    fprintf('   %3.4f\t \t  %3.4f \t\t  %3.4f \t\t   %3.4f\t\t  %3.4f \t  %3.4f \t  %3.4f \t  %3.4f\n', ...
        Real(i), Meas(i), Regression(i), Int(i), err(1, i), err(3, i), err(2, i), err(4,i));
end

% Funkcja Regresji liniowej
function [a, b, Y] = LinearRegression(x, y)
    x_avg = mean(x);
    y_avg = mean(y);
    a = sum(y .* (x - x_avg)) / sum((x - x_avg).^2);
    b = y_avg - x_avg * a;
    Y = a * x + b;
end
% Funkcja Interpolacji Lagrange'a
function Poly = LagrangeInterpolation(IntX, IntY)
    syms x;
    Int(x) = ...
        IntY(1) * (x - IntX(2))*(x - IntX(3))*(x - IntX(4)) / ((IntX(1) - IntX(2))*(IntX(1) - IntX(3))*(IntX(1) - IntX(4))) + ...
        IntY(2) * (x - IntX(1))*(x - IntX(3))*(x - IntX(4)) / ((IntX(2) - IntX(1))*(IntX(2) - IntX(3))*(IntX(2) - IntX(4))) + ...
        IntY(3) * (x - IntX(1))*(x - IntX(2))*(x - IntX(4)) / ((IntX(3) - IntX(1))*(IntX(3) - IntX(2))*(IntX(3) - IntX(4))) + ...
        IntY(4) * (x - IntX(1))*(x - IntX(2))*(x - IntX(3)) / ((IntX(4) - IntX(1))*(IntX(4) - IntX(2))*(IntX(4) - IntX(3)));
    Poly = sym2poly(Int);
end
% Funkcja wyznaczająca błędy pomiarowe
function [relative, absolute] = Error(Meas, Real)
    relative = Meas - Real;
    absolute = relative ./ Real * 100;
end

function [RetA, RetB, RetC] = FillVectors(Value, A, B, C, start, stop, maxSize)
    RetA = A; RetB = B; RetC = C;
    for i = 1:1:start - 1
        RetA = [Value RetA];
        RetB = [Value RetB];
        RetC = [Value RetC];
    end
    for i = stop:1:maxSize - 1
        RetA = [RetA Value];
        RetB = [RetB Value];
        RetC = [RetC Value];
    end
end

function DisplayErrors(X, rel, abs, title)
    figure('Name', title);
    subplot(2, 1, 1);
    plot(X, rel * 1E12);
    xlabel('pojemność rzeczywista [pF]');
    ylabel('błąd bezwzględny [pF]');
    subplot(2, 1, 2);
    plot(X, abs);
    xlabel('pojemność rzeczywista [pF]');
    ylabel('błąd względny [%]');
end